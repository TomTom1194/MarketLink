using System.Data;
using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class OrderService : IOrderService
    {
        public static readonly TimeSpan AutoAcceptDelay = TimeSpan.FromMinutes(5);
        private readonly MarketLinkDbContext _context;

        public OrderService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<OrderDto>> GetOrdersAsync(int farmerId, string? phone = null)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.Stall != null && o.Stall.FarmerId == farmerId);

            var trimmedPhone = phone?.Trim();
            if (!string.IsNullOrEmpty(trimmedPhone))
            {
                var phoneDigits = new string(trimmedPhone.Where(char.IsDigit).ToArray());
                if (phoneDigits.StartsWith("84") && phoneDigits.Length == 11)
                    phoneDigits = "0" + phoneDigits[2..];

                query = phoneDigits.Length switch
                {
                    3 or 4 => query.Where(order => order.PickupPhone.EndsWith(phoneDigits)),
                    10 when phoneDigits.StartsWith('0') => query.Where(order => order.PickupPhone == phoneDigits),
                    _ => query.Where(_ => false)
                };
            }

            var orders = await query
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .OrderByDescending(o => o.PlacedAt)
                .ToListAsync();

            return orders.Select(o => new OrderDto
            {
                Id = o.OrderId,
                OrderCode = o.OrderCode,
                CustomerName = o.PickupName,
                CustomerPhone = o.PickupPhone,
                ReceiveDate = o.PickupDate.Date.Add(o.PickupFrom),
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                Items = o.Items.OrderBy(item => item.SnapshotId).Select(item => new OrderItemDto
                {
                    ProductName = item.ProductName,
                    ImageUrl = item.ImageUrl,
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.LineTotal
                }).ToList()
            }).ToList();
        }

        public async Task<OrderDetailDto?> GetOrderDetailAsync(int id, int farmerId)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderId == id && o.Stall != null && o.Stall.FarmerId == farmerId)
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .FirstOrDefaultAsync();

            return order == null ? null : MapDetail(order);
        }

        public Task<bool> AcceptOrderAsync(int id, int farmerId) => AcceptOrderCoreAsync(id, farmerId, automated: false);

        public async Task<bool> AutoAcceptOrderAsync(int id)
        {
            try
            {
                return await AcceptOrderCoreAsync(id, null, automated: true);
            }
            catch (InvalidOperationException)
            {
                // The stock transaction has rolled back. A separate transaction records the warning once.
                _context.ChangeTracker.Clear();
                await RecordAutoAcceptFailureAsync(id);
                return false;
            }
        }

        public async Task<bool> GetAutoAcceptEnabledAsync(int farmerId)
        {
            return await _context.Stalls.AsNoTracking()
                .Where(stall => stall.FarmerId == farmerId && stall.IsActive)
                .Select(stall => stall.AutoAcceptEnabled)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> SetAutoAcceptEnabledAsync(int farmerId, bool enabled)
        {
            var stall = await _context.Stalls.FirstOrDefaultAsync(item => item.FarmerId == farmerId && item.IsActive);
            if (stall == null) return false;

            stall.AutoAcceptEnabled = enabled;
            stall.AutoAcceptEnabledAt = enabled ? DateTime.Now : null;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> AcceptOrderCoreAsync(int id, int? farmerId, bool automated)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await GetTrackedOrderAsync(id, farmerId);
            if (order == null || !IsStatus(order, "placed")) return false;

            if (automated)
            {
                var stall = order.Stall!;
                if (!stall.IsActive || stall.Farmer?.ApprovalStatus != "approved" ||
                    !stall.AutoAcceptEnabled || stall.AutoAcceptEnabledAt == null ||
                    order.PlacedAt < stall.AutoAcceptEnabledAt || order.AutoAcceptFailedAt != null ||
                    order.PlacedAt > DateTime.Now - AutoAcceptDelay) return false;
            }

            // Both the button and the background worker reserve stock before accepting the order.
            await ReserveStockAsync(order);
            order.Status = "accepted";
            order.AcceptedAt = DateTime.Now;
            AddCustomerNotification(order, "order_accepted", $"Order #{order.OrderCode} was accepted", automated
                ? "The stall automatically accepted your order. Your products are reserved for pickup."
                : "The farmer accepted your order. See you at the pickup time you chose.");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        private async Task RecordAutoAcceptFailureAsync(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await GetTrackedOrderAsync(id, null);
            if (order == null || !IsStatus(order, "placed") || order.AutoAcceptFailedAt != null ||
                order.Stall?.AutoAcceptEnabled != true) return;

            order.AutoAcceptFailedAt = DateTime.Now;
            _context.Notifications.Add(new Notification
            {
                UserId = order.Stall.FarmerId,
                OrderId = order.OrderId,
                Type = "new_order",
                Title = $"Order #{order.OrderCode} needs your attention",
                Body = "Automatic acceptance could not reserve enough stock. The order is still waiting for your decision.",
                CreatedAt = DateTime.Now
            });
            _context.Notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                OrderId = order.OrderId,
                Type = "new_order",
                Title = $"Order #{order.OrderCode} is still waiting",
                Body = "The stall could not confirm your order automatically. The farmer will review it.",
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<bool> RejectOrderAsync(int id, int farmerId, string reason)
        {
            reason = reason.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return false;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await GetTrackedOrderAsync(id, farmerId);
            if (order == null || !IsStatus(order, "placed")) return false;

            // A placed order has not taken any stock yet, so nothing to give back
            order.Status = "rejected";
            order.RejectReason = reason;
            order.RejectedAt = DateTime.Now;
            AddCustomerNotification(order, "order_rejected", $"Order #{order.OrderCode} was rejected", $"Reason: {reason}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public async Task<bool> CompleteOrderAsync(int id, int farmerId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await GetTrackedOrderAsync(id, farmerId);
            if (order == null || !IsStatus(order, "accepted")) return false;

            // Step 3: the customer picked up the products, so the reserved quantity is now sold
            await MarkStockSoldAsync(order);
            order.Status = "completed";
            order.CompletedAt = DateTime.Now;
            order.CompletedBy = farmerId;
            AddCustomerNotification(order, "order_completed", $"Order #{order.OrderCode} is complete", "The farmer confirmed that you picked up all the products.");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        private Task<Order?> GetTrackedOrderAsync(int id, int? farmerId)
        {
            var query = _context.Orders.Where(o => o.OrderId == id).Include(o => o.Items)
                .Include(o => o.Stall).ThenInclude(stall => stall!.Farmer).AsQueryable();
            if (farmerId.HasValue) query = query.Where(o => o.Stall != null && o.Stall.FarmerId == farmerId.Value);
            return query.FirstOrDefaultAsync();
        }

        // Step 2 (accept): move the ordered quantity into quantity_reserved,
        // so it is no longer available to other customers.
        private async Task ReserveStockAsync(Order order)
        {
            foreach (var item in order.Items)
            {
                // The price row saved with the order may have been closed since
                // (the farmer changed the price). Then use the row that is on sale now.
                var stock = await _context.StockPrices.AsNoTracking().FirstOrDefaultAsync(sp => sp.StockPriceId == item.StockPriceId);
                if (stock == null || stock.EffectiveTo != null)
                {
                    stock = await _context.StockPrices.AsNoTracking()
                        .Where(sp => sp.ProductId == item.ProductId && sp.StallId == order.StallId && sp.EffectiveTo == null)
                        .OrderByDescending(sp => sp.EffectiveFrom)
                        .FirstOrDefaultAsync();
                }

                if (stock == null || item.Quantity <= 0)
                {
                    throw new InvalidOperationException($"No valid stock is available for {item.ProductName}. The order is still waiting for a decision.");
                }

                // One conditional SQL update checks and reserves the stock atomically.
                int updated = await _context.StockPrices
                    .Where(sp => sp.StockPriceId == stock.StockPriceId && sp.StallId == order.StallId &&
                        sp.EffectiveTo == null && sp.QuantityIn - sp.QuantityReserved - sp.QuantitySold >= item.Quantity)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(sp => sp.QuantityReserved, sp => sp.QuantityReserved + item.Quantity));
                if (updated != 1)
                {
                    throw new InvalidOperationException($"Not enough stock for {item.ProductName}. The order is still waiting for a decision.");
                }

                // Remember which row holds the reservation (the price the customer pays does not change)
                item.StockPriceId = stock.StockPriceId;
            }
        }

        // Step 3 (confirm pickup): the reserved quantity becomes sold.
        private async Task MarkStockSoldAsync(Order order)
        {
            foreach (var item in order.Items)
            {
                var stock = await _context.StockPrices.FirstOrDefaultAsync(sp => sp.StockPriceId == item.StockPriceId);
                if (stock == null || stock.QuantityReserved < item.Quantity)
                {
                    throw new InvalidOperationException($"Not enough reserved stock to complete order #{order.OrderCode}. Please check your stock.");
                }

                stock.QuantityReserved -= item.Quantity;
                stock.QuantitySold += item.Quantity;
            }
        }

        private void AddCustomerNotification(Order order, string type, string title, string message)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                OrderId = order.OrderId,
                Type = type,
                Title = title,
                Body = message,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
        }

        private static bool IsStatus(Order order, string status) =>
            string.Equals(order.Status, status, StringComparison.OrdinalIgnoreCase);

        private static OrderDetailDto MapDetail(Order order)
        {
            var productTotal = order.Items.Sum(item => item.LineTotal);
            return new OrderDetailDto
            {
                Id = order.OrderId,
                OrderCode = order.OrderCode,
                CustomerName = order.PickupName,
                CustomerPhone = order.PickupPhone,
                Address = order.Customer?.Address ?? string.Empty,
                ReceiveDate = order.PickupDate.Date.Add(order.PickupFrom),
                PickupEnd = order.PickupDate.Date.Add(order.PickupTo),
                TotalProductAmount = productTotal,
                ShippingFee = order.TotalAmount - productTotal,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                RejectReason = order.RejectReason,
                CancelReason = order.CancelReason,
                Items = order.Items.OrderBy(item => item.SnapshotId).Select(MapItem).ToList()
            };
        }

        private static OrderItemDto MapItem(OrderSnapshot item) => new()
        {
            ProductName = item.ProductName,
            ImageUrl = item.ImageUrl,
            Unit = item.Unit,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.LineTotal
        };
    }
}
