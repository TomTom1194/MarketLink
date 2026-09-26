using System.Data;
using MarketLink.Data;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class CustomerOrderService : ICustomerOrderService
    {
        private readonly MarketLinkDbContext _context;

        public CustomerOrderService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<Order>> GetOrdersAsync(int customerId, string? status)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Stall)
                .ThenInclude(s => s!.Market)
                .Include(o => o.Stall)
                .ThenInclude(s => s!.Farmer)
                .Where(o => o.CustomerId == customerId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            return await query.OrderByDescending(o => o.PlacedAt).ToListAsync();
        }

        public async Task<Order?> GetOrderDetailAsync(int customerId, int orderId)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Stall)
                .ThenInclude(s => s!.Market)
                .ThenInclude(m => m!.District)
                .Include(o => o.Stall)
                .ThenInclude(s => s!.Farmer)
                .ThenInclude(f => f!.User)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);
        }

        public async Task<string> CancelOrderAsync(int customerId, int orderId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return "Please enter a reason for cancelling.";
            }

            if (reason.Trim().Length > 500)
            {
                return "Reason must be at most 500 characters.";
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _context.Orders
                .Include(o => o.Stall)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null)
            {
                return "Order not found.";
            }

            if (order.Status != "placed" && order.Status != "accepted")
            {
                return "This order can no longer be cancelled.";
            }

            if (order.Status == "accepted")
            {
                var quantities = order.Items.GroupBy(item => item.StockPriceId)
                    .Select(group => new { StockPriceId = group.Key, Quantity = group.Sum(item => item.Quantity) });
                foreach (var line in quantities)
                {
                    var updated = await _context.StockPrices
                        .Where(stock => stock.StockPriceId == line.StockPriceId && stock.StallId == order.StallId && stock.QuantityReserved >= line.Quantity)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(stock => stock.QuantityReserved, stock => stock.QuantityReserved - line.Quantity));
                    if (updated != 1) return "Reserved stock could not be released. Please contact the farmer.";
                }
            }

            order.Status = "cancelled";
            order.CancelReason = reason.Trim();
            order.CancelledAt = DateTime.Now;

            _context.Notifications.Add(new Notification
            {
                UserId = order.Stall!.FarmerId,
                OrderId = order.OrderId,
                Type = "order_cancelled",
                Title = "Order " + order.OrderCode + " was cancelled",
                Body = "Reason: " + order.CancelReason
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return "";
        }
    }
}
