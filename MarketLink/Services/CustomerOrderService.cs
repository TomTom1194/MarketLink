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

            var order = await _context.Orders
                .Include(o => o.Stall)
                .Include(o => o.Items)
                .ThenInclude(i => i.StockPrice)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null)
            {
                return "Order not found.";
            }

            if (order.Status != "placed" && order.Status != "accepted")
            {
                return "This order can no longer be cancelled.";
            }

            foreach (var item in order.Items)
            {
                var sp = item.StockPrice!;
                sp.QuantityReserved = sp.QuantityReserved - item.Quantity;
                if (sp.QuantityReserved < 0)
                {
                    sp.QuantityReserved = 0;
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
            return "";
        }
    }
}
