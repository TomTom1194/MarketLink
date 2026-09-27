using System.Data;
using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class OrderService : IOrderService
    {
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

        public async Task<bool> AcceptOrderAsync(int id, int farmerId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await GetTrackedOrderAsync(id, farmerId);
            if (order == null || !IsStatus(order, "placed")) return false;

            // Step 2: stock is taken only when the farmer accepts the order
            await ReserveStockAsync(order);
            order.Status = "accepted";
            order.AcceptedAt = DateTime.Now;
            AddCustomerNotification(order, "order_accepted", $"Order #{order.OrderCode} was accepted", "The farmer accepted your order. See you at the pickup time you chose.");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
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

        private Task<Order?> GetTrackedOrderAsync(int id, int farmerId) => _context.Orders
            .Where(o => o.OrderId == id && o.Stall != null && o.Stall.FarmerId == farmerId)
            .Include(o => o.Items)
            .FirstOrDefaultAsync();

        // Step 2 (accept): move the ordered quantity into quantity_reserved,
        // so it is no longer available to other customers.
        private async Task ReserveStockAsync(Order order)
        {
            foreach (var item in order.Items)
            {
                // The price row saved with the order may have been closed since
                // (the farmer changed the price). Then use the row that is on sale now.
                var stock = await _context.StockPrices.FirstOrDefaultAsync(sp => sp.StockPriceId == item.StockPriceId);
                if (stock == null || stock.EffectiveTo != null)
                {
                    stock = await _context.StockPrices
                        .Where(sp => sp.ProductId == item.ProductId && sp.StallId == order.StallId && sp.EffectiveTo == null)
                        .OrderByDescending(sp => sp.EffectiveFrom)
                        .FirstOrDefaultAsync();
                }

                if (stock == null || stock.QuantityAvailable < item.Quantity)
                {
                    decimal left = stock == null ? 0 : stock.QuantityAvailable;
                    throw new InvalidOperationException(
                        $"Not enough stock for {item.ProductName}: the order needs {item.Quantity:0.##} {item.Unit} but only {left:0.##} {item.Unit} are left. Reject the order or re-up the product first.");
                }

                stock.QuantityReserved += item.Quantity;

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
