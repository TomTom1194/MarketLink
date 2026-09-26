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

        public async Task<FarmerStatisticsDto> GetFarmerStatisticsAsync(int farmerId)
        {
            var farmerOrders = _context.Orders.AsNoTracking()
                .Where(order => order.Stall != null && order.Stall.FarmerId == farmerId);

            var pending = await farmerOrders.CountAsync(order => order.Status == "placed" || order.Status == "accepted");
            var revenue = await farmerOrders.Where(order => order.Status == "completed")
                .Select(order => (decimal?)order.TotalAmount).SumAsync() ?? 0m;
            var bestSelling = await _context.OrderSnapshots.AsNoTracking()
                .Where(item => item.Order != null && item.Order.Stall != null &&
                    item.Order.Stall.FarmerId == farmerId && item.Order.Status == "completed")
                .GroupBy(item => item.ProductName)
                .Select(group => new BestSellingProductDto
                {
                    ProductName = group.Key,
                    QuantitySold = group.Sum(item => item.Quantity),
                    Revenue = group.Sum(item => item.LineTotal)
                })
                .OrderByDescending(item => item.QuantitySold)
                .Take(5)
                .ToListAsync();

            return new FarmerStatisticsDto
            {
                PendingOrders = pending,
                Revenue = revenue,
                BestSellingProducts = bestSelling
            };
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

            order.Status = "accepted";
            order.AcceptedAt = DateTime.Now;
            AddCustomerNotification(order, "order_accepted", $"Đơn hàng #{order.OrderCode} đã được xác nhận", "Nông dân đã xác nhận đơn hàng của bạn. Hẹn gặp bạn vào thời gian nhận hàng đã chọn.");
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

            await AdjustReservedStockAsync(order, complete: false);
            order.Status = "rejected";
            order.RejectReason = reason;
            order.RejectedAt = DateTime.Now;
            AddCustomerNotification(order, "order_rejected", $"Đơn hàng #{order.OrderCode} đã bị từ chối", $"Lý do: {reason}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public async Task<bool> CancelFarmerOrderAsync(int id, int farmerId, string reason)
        {
            reason = reason?.Trim() ?? string.Empty;
            if (reason.Length == 0 || reason.Length > 500) return false;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await GetTrackedOrderAsync(id, farmerId);
            if (order == null || !(IsStatus(order, "placed") || IsStatus(order, "accepted"))) return false;

            await AdjustReservedStockAsync(order, complete: false);
            order.Status = "cancelled";
            order.CancelReason = reason;
            order.CancelledAt = DateTime.Now;
            AddCustomerNotification(order, "order_cancelled", $"Đơn hàng #{order.OrderCode} đã bị hủy", $"Lý do: {reason}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public async Task<bool> CompleteOrderAsync(int id, int farmerId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await GetTrackedOrderAsync(id, farmerId);
            if (order == null || !IsStatus(order, "accepted")) return false;

            await AdjustReservedStockAsync(order, complete: true);
            order.Status = "completed";
            order.CompletedAt = DateTime.Now;
            order.CompletedBy = farmerId;
            AddCustomerNotification(order, "order_completed", $"Đơn hàng #{order.OrderCode} đã hoàn thành", "Nông dân đã xác nhận bạn nhận đủ sản phẩm.");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        private Task<Order?> GetTrackedOrderAsync(int id, int farmerId) => _context.Orders
            .Where(o => o.OrderId == id && o.Stall != null && o.Stall.FarmerId == farmerId)
            .Include(o => o.Items)
            .FirstOrDefaultAsync();

        private async Task AdjustReservedStockAsync(Order order, bool complete)
        {
            var quantities = order.Items
                .GroupBy(item => item.StockPriceId)
                .Select(group => new { StockPriceId = group.Key, Quantity = group.Sum(item => item.Quantity) })
                .ToList();

            var stockIds = quantities.Select(x => x.StockPriceId).ToList();
            var stockRows = await _context.StockPrices
                .Where(stock => stockIds.Contains(stock.StockPriceId))
                .ToDictionaryAsync(stock => stock.StockPriceId);

            foreach (var line in quantities)
            {
                if (!stockRows.TryGetValue(line.StockPriceId, out var stock) || stock.QuantityReserved < line.Quantity)
                {
                    throw new InvalidOperationException($"Không đủ số lượng đã giữ để xử lý đơn #{order.OrderCode}. Vui lòng kiểm tra lại tồn kho.");
                }

                stock.QuantityReserved -= line.Quantity;
                if (complete) stock.QuantitySold += line.Quantity;
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
