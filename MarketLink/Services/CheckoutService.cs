using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Helpers;
using MarketLink.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class CheckoutService : ICheckoutService
    {
        private readonly MarketLinkDbContext _context;

        public CheckoutService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerProfile?> GetCustomerAsync(int customerId)
        {
            return await _context.CustomerProfiles
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }

        public List<DateTime> GetPickupDates(Stall stall, Market market)
        {
            var dates = new List<DateTime>();
            List<string> marketDays = market.OpenDays.Split(',').Select(x => x.Trim()).ToList();
            List<string> stallDays = stall.SellingDays.Split(',').Select(x => x.Trim()).ToList();

            for (int i = 0; i < 7; i++)
            {
                DateTime day = DateTime.Today.AddDays(i);

                if (i == 0 && DateTime.Now.TimeOfDay >= market.CloseTime)
                {
                    continue;
                }

                int dayNumber = (int)day.DayOfWeek + 1;
                if (day.DayOfWeek == DayOfWeek.Sunday)
                {
                    dayNumber = 8;
                }

                if (marketDays.Contains(dayNumber.ToString()) && stallDays.Contains(dayNumber.ToString()))
                {
                    dates.Add(day);
                }
            }

            return dates;
        }

        public List<DateTime> GetCommonPickupDates(List<Stall> stalls, Market market)
        {
            var dates = new List<DateTime>();
            if (stalls.Count == 0)
            {
                return dates;
            }

            dates = GetPickupDates(stalls[0], market);
            for (int i = 1; i < stalls.Count; i++)
            {
                List<DateTime> stallDates = GetPickupDates(stalls[i], market);
                dates = dates.Where(d => stallDates.Contains(d)).ToList();
            }
            return dates;
        }

        private string CheckPickupTime(DateTime? pickupDate, string? pickupSlot, List<DateTime> validDates, Market market, out TimeSpan pickupFrom, out TimeSpan pickupTo)
        {
            pickupFrom = TimeSpan.Zero;
            pickupTo = TimeSpan.Zero;

            if (pickupDate == null || !validDates.Contains(pickupDate.Value.Date))
            {
                return "The selected pickup day is not available. Please choose another day.";
            }

            string[] slotParts = (pickupSlot ?? "").Split('-');
            if (slotParts.Length != 2 || !TimeSpan.TryParse(slotParts[0], out pickupFrom) || !TimeSpan.TryParse(slotParts[1], out pickupTo))
            {
                return "Please choose a pickup time.";
            }

            if (pickupFrom >= pickupTo || pickupFrom < market.OpenTime || pickupTo > market.CloseTime)
            {
                return "The pickup time must be within market hours.";
            }

            if (pickupDate.Value.Date == DateTime.Today && pickupTo <= DateTime.Now.TimeOfDay)
            {
                return "This pickup time has already passed. Please choose a later time.";
            }

            return "";
        }

        public async Task<CheckoutResultDto> ReserveNowAsync(int customerId, QuickReserveDto model)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    return await TryReserveNowAsync(customerId, model);
                }
                catch (SqlException ex)
                {
                    if (ex.Number != 1205)
                    {
                        throw;
                    }
                    _context.ChangeTracker.Clear();
                }
            }
            return BusyResult();
        }

        public async Task<CheckoutResultDto> PlaceOrdersAsync(int customerId, CheckoutDto model)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    return await TryPlaceOrdersAsync(customerId, model);
                }
                catch (SqlException ex)
                {
                    if (ex.Number != 1205)
                    {
                        throw;
                    }
                    _context.ChangeTracker.Clear();
                }
            }
            return BusyResult();
        }

        private CheckoutResultDto BusyResult()
        {
            var result = new CheckoutResultDto();
            result.Errors.Add("Many customers are ordering at the same time. Please check My orders, then try again if needed.");
            return result;
        }

        private async Task<CheckoutResultDto> TryReserveNowAsync(int customerId, QuickReserveDto model)
        {
            var result = new CheckoutResultDto();

            string quantityError = ShopHelper.CheckQuantity(model.Quantity);
            if (quantityError != "")
            {
                result.Errors.Add(quantityError);
                return result;
            }

            var customer = await GetCustomerAsync(customerId);
            if (customer == null)
            {
                result.Errors.Add("Customer profile not found.");
                return result;
            }

            var sp = await _context.StockPrices
                .Include(x => x.Product)
                .ThenInclude(p => p!.Category)
                .Include(x => x.Stall)
                .ThenInclude(s => s!.Market)
                .Include(x => x.Stall)
                .ThenInclude(s => s!.Farmer)
                .FirstOrDefaultAsync(x => x.StockPriceId == model.StockPriceId);

            if (sp == null || sp.EffectiveTo != null || sp.Product!.Status != "active" || sp.Product.ExpiresAt <= DateTime.Now
                || !sp.Stall!.IsActive || sp.Stall.Farmer!.ApprovalStatus != "approved")
            {
                result.Errors.Add("This product is no longer on sale.");
                return result;
            }

            if (model.Quantity > sp.QuantityAvailable)
            {
                result.Errors.Add("Only " + sp.QuantityAvailable.ToString("0.##") + " " + sp.Product.Unit + " left.");
                return result;
            }

            var stall = sp.Stall;
            var market = stall.Market!;

            TimeSpan pickupFrom;
            TimeSpan pickupTo;
            string pickupError = CheckPickupTime(model.PickupDate, model.PickupSlot, GetPickupDates(stall, market), market, out pickupFrom, out pickupTo);
            if (pickupError != "")
            {
                result.Errors.Add(pickupError);
                return result;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            await LockCustomerAsync(customerId);

            DateTime recentTime = DateTime.Now.AddSeconds(-30);
            bool justReserved = await _context.Orders.AnyAsync(o => o.CustomerId == customerId
                && o.Status == "placed"
                && o.PlacedAt >= recentTime
                && o.Items.Any(i => i.StockPriceId == sp.StockPriceId && i.Quantity == model.Quantity));
            if (justReserved)
            {
                result.Errors.Add("You have just reserved this product. Please check My orders.");
                return result;
            }

            var order = new Order
            {
                CustomerId = customerId,
                StallId = stall.StallId,
                PickupDate = model.PickupDate!.Value.Date,
                PickupFrom = pickupFrom,
                PickupTo = pickupTo,
                PickupName = customer.FullName,
                PickupPhone = customer.User!.Phone,
                Status = "placed",
                PlacedAt = DateTime.Now,
                TotalAmount = sp.Price * model.Quantity
            };

            order.Items.Add(new OrderSnapshot
            {
                ProductId = sp.ProductId,
                StockPriceId = sp.StockPriceId,
                ProductName = sp.Product.ProductName,
                CategoryName = sp.Product.Category!.CategoryName,
                Unit = sp.Product.Unit,
                ImageUrl = sp.Product.ImageUrl,
                UnitPrice = sp.Price,
                Quantity = model.Quantity
            });

            _context.Orders.Add(order);

            var notification = new Notification
            {
                UserId = stall.FarmerId,
                Order = order,
                Type = "new_order",
                Body = order.PickupName + " reserved " + model.Quantity.ToString("0.##") + " " + sp.Product.Unit + " of " + sp.Product.ProductName
                    + ". Pickup on " + order.PickupDate.ToString("dd/MM/yyyy") + ", " + pickupFrom.ToString(@"hh\:mm") + " - " + pickupTo.ToString(@"hh\:mm") + "."
            };
            _context.Notifications.Add(notification);

            string saveError = await SaveOrdersAsync(new List<Order> { order }, new List<Notification> { notification });
            if (saveError != "")
            {
                result.Errors.Add(saveError);
                return result;
            }

            await transaction.CommitAsync();
            result.Orders.Add(order);
            return result;
        }

        private async Task<CheckoutResultDto> TryPlaceOrdersAsync(int customerId, CheckoutDto model)
        {
            var result = new CheckoutResultDto();

            var cart = await _context.Carts
                .Include(c => c.Market)
                .Include(c => c.Items)
                .ThenInclude(i => i.StockPrice)
                .ThenInclude(sp => sp!.Product)
                .ThenInclude(p => p!.Category)
                .Include(c => c.Items)
                .ThenInclude(i => i.StockPrice)
                .ThenInclude(sp => sp!.Stall)
                .ThenInclude(s => s!.Farmer)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.MarketId == model.MarketId);

            if (cart == null || cart.Items.Count == 0)
            {
                result.Errors.Add("Your basket is empty.");
                return result;
            }

            var itemsByStall = new Dictionary<int, List<CartItem>>();

            foreach (var item in cart.Items)
            {
                var sp = item.StockPrice!;

                string status = ShopHelper.ItemStatus(sp);
                if (status != "ok")
                {
                    if (!model.SkippedItemIds.Contains(item.CartItemId))
                    {
                        string reason = status == "sold_out" ? "has just sold out" : "is no longer available";
                        result.Errors.Add(sp.Product!.ProductName + " " + reason + ". Please check your basket again before ordering.");
                    }
                    continue;
                }

                string quantityError = ShopHelper.CheckQuantity(item.Quantity);
                if (quantityError != "")
                {
                    result.Errors.Add(sp.Product!.ProductName + ": " + quantityError);
                    continue;
                }

                if (item.Quantity > sp.QuantityAvailable)
                {
                    result.Errors.Add(sp.Product!.ProductName + ": only " + sp.QuantityAvailable.ToString("0.##") + " " + sp.Product.Unit + " left. Please check your basket again.");
                    continue;
                }

                if (!itemsByStall.ContainsKey(sp.StallId))
                {
                    itemsByStall[sp.StallId] = new List<CartItem>();
                }
                itemsByStall[sp.StallId].Add(item);
            }

            if (result.Errors.Count == 0 && itemsByStall.Count == 0)
            {
                result.Errors.Add("None of the items in your basket can be ordered right now.");
                return result;
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            var stalls = new List<Stall>();
            foreach (int stallId in itemsByStall.Keys)
            {
                stalls.Add(itemsByStall[stallId][0].StockPrice!.Stall!);
            }

            List<DateTime> commonDates = GetCommonPickupDates(stalls, cart.Market!);
            if (commonDates.Count == 0)
            {
                result.Errors.Add("The farmers in your basket have no common selling day in the next 7 days. Please remove some items and order them separately.");
                return result;
            }

            TimeSpan pickupFrom;
            TimeSpan pickupTo;
            string pickupError = CheckPickupTime(model.PickupDate, model.PickupSlot, commonDates, cart.Market!, out pickupFrom, out pickupTo);
            if (pickupError != "")
            {
                result.Errors.Add(pickupError);
                return result;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            await LockCustomerAsync(customerId);

            var orders = new List<Order>();
            var notifications = new List<Notification>();

            foreach (int stallId in itemsByStall.Keys)
            {
                var stall = itemsByStall[stallId][0].StockPrice!.Stall!;

                var order = new Order
                {
                    CustomerId = customerId,
                    StallId = stallId,
                    PickupDate = model.PickupDate!.Value.Date,
                    PickupFrom = pickupFrom,
                    PickupTo = pickupTo,
                    PickupName = model.PickupName.Trim(),
                    PickupPhone = model.PickupPhone.Trim(),
                    CustomerNote = string.IsNullOrWhiteSpace(model.CustomerNote) ? null : model.CustomerNote.Trim(),
                    Status = "placed",
                    PlacedAt = DateTime.Now
                };

                decimal totalAmount = 0;
                foreach (var item in itemsByStall[stallId])
                {
                    var sp = item.StockPrice!;

                    order.Items.Add(new OrderSnapshot
                    {
                        ProductId = sp.ProductId,
                        StockPriceId = sp.StockPriceId,
                        ProductName = sp.Product!.ProductName,
                        CategoryName = sp.Product.Category!.CategoryName,
                        Unit = sp.Product.Unit,
                        ImageUrl = sp.Product.ImageUrl,
                        UnitPrice = sp.Price,
                        Quantity = item.Quantity
                    });

                    totalAmount = totalAmount + sp.Price * item.Quantity;
                }
                order.TotalAmount = totalAmount;

                _context.Orders.Add(order);
                orders.Add(order);

                var notification = new Notification
                {
                    UserId = stall.FarmerId,
                    Order = order,
                    Type = "new_order",
                    Body = order.PickupName + " reserved " + order.Items.Count + " item(s), total $" + totalAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + ". Pickup on " + order.PickupDate.ToString("dd/MM/yyyy") + ", " + pickupFrom.ToString(@"hh\:mm") + " - " + pickupTo.ToString(@"hh\:mm") + "."
                };
                _context.Notifications.Add(notification);
                notifications.Add(notification);
            }

            foreach (int stallId in itemsByStall.Keys)
            {
                _context.CartItems.RemoveRange(itemsByStall[stallId]);
            }
            cart.UpdatedAt = DateTime.Now;

            string saveError = await SaveOrdersAsync(orders, notifications);
            if (saveError != "")
            {
                result.Errors.Add(saveError);
                return result;
            }

            await transaction.CommitAsync();
            result.Orders.AddRange(orders);
            return result;
        }

        private async Task LockCustomerAsync(int customerId)
        {
            await _context.CustomerProfiles
                .Where(c => c.CustomerId == customerId)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.FullName, c => c.FullName));
        }

        private async Task<string> SaveOrdersAsync(List<Order> orders, List<Notification> notifications)
        {
            string codePrefix = "ML-" + DateTime.Now.ToString("yyMMdd") + "-";

            for (int attempt = 1; attempt <= 5; attempt++)
            {
                int lastNumber = await GetLastOrderNumberAsync(codePrefix);
                for (int i = 0; i < orders.Count; i++)
                {
                    lastNumber++;
                    orders[i].OrderCode = codePrefix + lastNumber.ToString("D4");
                    notifications[i].Title = "New order " + orders[i].OrderCode;
                }

                try
                {
                    await _context.SaveChangesAsync();
                    return "";
                }
                catch (DbUpdateConcurrencyException)
                {
                    return "Your basket has just been ordered or changed. Please check My orders before trying again.";
                }
                catch (DbUpdateException ex)
                {
                    var sqlError = ex.InnerException as SqlException;
                    if (sqlError != null && sqlError.Number == 1205)
                    {
                        throw sqlError;
                    }
                    if (!IsDuplicateKeyError(ex))
                    {
                        return "Could not save your order. Please try again.";
                    }
                }
            }

            return "The system is busy right now. Please try again in a moment.";
        }

        private async Task<int> GetLastOrderNumberAsync(string codePrefix)
        {
            string? lastCode = await _context.Orders
                .Where(o => o.OrderCode.StartsWith(codePrefix))
                .OrderByDescending(o => o.OrderCode.Length)
                .ThenByDescending(o => o.OrderCode)
                .Select(o => o.OrderCode)
                .FirstOrDefaultAsync();

            int lastNumber = 0;
            if (lastCode != null)
            {
                int.TryParse(lastCode.Substring(codePrefix.Length), out lastNumber);
            }
            return lastNumber;
        }

        private bool IsDuplicateKeyError(DbUpdateException ex)
        {
            var sqlError = ex.InnerException as SqlException;
            return sqlError != null && (sqlError.Number == 2627 || sqlError.Number == 2601);
        }
    }
}
