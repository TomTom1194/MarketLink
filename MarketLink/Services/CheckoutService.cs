using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Helpers;
using MarketLink.Models;
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

        public DateTime? GetNextPickupDate(Stall stall, Market market)
        {
            List<DateTime> dates = GetPickupDates(stall, market);
            if (dates.Count == 0)
            {
                return null;
            }
            return dates[0];
        }

        public async Task<CheckoutResultDto> ReserveNowAsync(int customerId, QuickReserveDto model)
        {
            var result = new CheckoutResultDto();

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

            if (model.Quantity <= 0)
            {
                result.Errors.Add("Quantity must be greater than 0.");
                return result;
            }

            if (model.Quantity > sp.QuantityAvailable)
            {
                result.Errors.Add("Only " + sp.QuantityAvailable.ToString("0.##") + " " + sp.Product.Unit + " left.");
                return result;
            }

            var stall = sp.Stall;
            var market = stall.Market!;

            List<DateTime> validDates = GetPickupDates(stall, market);
            if (!validDates.Contains(model.PickupDate.Date))
            {
                result.Errors.Add("The stall does not sell on the selected day. Please choose another day.");
                return result;
            }

            string[] slotParts = (model.PickupSlot ?? "").Split('-');
            TimeSpan pickupFrom;
            TimeSpan pickupTo;
            if (slotParts.Length != 2 || !TimeSpan.TryParse(slotParts[0], out pickupFrom) || !TimeSpan.TryParse(slotParts[1], out pickupTo))
            {
                result.Errors.Add("Please choose a pickup time.");
                return result;
            }

            if (pickupFrom >= pickupTo || pickupFrom < market.OpenTime || pickupTo > market.CloseTime)
            {
                result.Errors.Add("The pickup time must be within market hours.");
                return result;
            }

            if (model.PickupDate.Date == DateTime.Today && pickupTo <= DateTime.Now.TimeOfDay)
            {
                result.Errors.Add("This pickup time has already passed. Please choose a later time.");
                return result;
            }

            string codePrefix = "ML-" + DateTime.Now.ToString("yyMMdd") + "-";
            int todayOrderCount = await _context.Orders.CountAsync(o => o.OrderCode.StartsWith(codePrefix));

            var order = new Order
            {
                OrderCode = codePrefix + (todayOrderCount + 1).ToString("D4"),
                CustomerId = customerId,
                StallId = stall.StallId,
                PickupDate = model.PickupDate.Date,
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

            sp.QuantityReserved = sp.QuantityReserved + model.Quantity;

            _context.Orders.Add(order);

            _context.Notifications.Add(new Notification
            {
                UserId = stall.FarmerId,
                Order = order,
                Type = "new_order",
                Title = "New order " + order.OrderCode,
                Body = order.PickupName + " reserved " + model.Quantity.ToString("0.##") + " " + sp.Product.Unit + " of " + sp.Product.ProductName
                    + ". Pickup on " + order.PickupDate.ToString("dd/MM/yyyy") + ", " + pickupFrom.ToString(@"hh\:mm") + " - " + pickupTo.ToString(@"hh\:mm") + "."
            });

            try
            {
                await _context.SaveChangesAsync();
                result.Orders.Add(order);
            }
            catch (DbUpdateException)
            {
                result.Errors.Add("This product was just reserved by other customers. Please try again.");
            }

            return result;
        }

        public async Task<CheckoutResultDto> PlaceOrdersAsync(int customerId, CheckoutDto model)
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

                if (ShopHelper.ItemStatus(sp) != "ok")
                {
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

            var pickupDateByStall = new Dictionary<int, DateTime>();
            foreach (int stallId in itemsByStall.Keys)
            {
                var stall = itemsByStall[stallId][0].StockPrice!.Stall!;
                DateTime? pickupDate = GetNextPickupDate(stall, cart.Market!);

                if (pickupDate == null)
                {
                    result.Errors.Add("Stall " + stall.StallCode + " has no selling day in the next 7 days.");
                }
                else
                {
                    pickupDateByStall[stallId] = pickupDate.Value;
                }
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            string codePrefix = "ML-" + DateTime.Now.ToString("yyMMdd") + "-";
            int todayOrderCount = await _context.Orders.CountAsync(o => o.OrderCode.StartsWith(codePrefix));

            foreach (int stallId in itemsByStall.Keys)
            {
                var stall = itemsByStall[stallId][0].StockPrice!.Stall!;
                todayOrderCount++;

                var order = new Order
                {
                    OrderCode = codePrefix + todayOrderCount.ToString("D4"),
                    CustomerId = customerId,
                    StallId = stallId,
                    PickupDate = pickupDateByStall[stallId],
                    PickupFrom = cart.Market!.OpenTime,
                    PickupTo = cart.Market.CloseTime,
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

                    sp.QuantityReserved = sp.QuantityReserved + item.Quantity;
                    totalAmount = totalAmount + sp.Price * item.Quantity;
                }
                order.TotalAmount = totalAmount;

                _context.Orders.Add(order);

                _context.Notifications.Add(new Notification
                {
                    UserId = stall.FarmerId,
                    Order = order,
                    Type = "new_order",
                    Title = "New order " + order.OrderCode,
                    Body = order.PickupName + " reserved " + order.Items.Count + " item(s), total $" + totalAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + ". Pickup on " + order.PickupDate.ToString("dd/MM/yyyy") + "."
                });

                result.Orders.Add(order);
            }

            foreach (int stallId in itemsByStall.Keys)
            {
                _context.CartItems.RemoveRange(itemsByStall[stallId]);
            }
            cart.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                result.Errors.Add("Some items were just reserved by other customers. Please check your basket and try again.");
                result.Orders.Clear();
            }

            return result;
        }
    }
}
