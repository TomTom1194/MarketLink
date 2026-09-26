using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.Data.SqlClient;
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

        public async Task<List<PickupDayDto>> GetPickupScheduleAsync(int customerId)
        {
            DateTime today = DateTime.Today;
            TimeSpan now = DateTime.Now.TimeOfDay;

            var orders = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Stall)
                .ThenInclude(s => s!.Market)
                .Include(o => o.Stall)
                .ThenInclude(s => s!.Farmer)
                .Where(o => o.CustomerId == customerId && o.Status == "accepted" && o.PickupDate >= today)
                .OrderBy(o => o.PickupDate)
                .ThenBy(o => o.PickupFrom)
                .ThenBy(o => o.PickupTo)
                .ToListAsync();

            var days = new List<PickupDayDto>();

            foreach (var order in orders)
            {
                if (order.PickupDate == today && order.PickupTo <= now)
                {
                    continue;
                }

                PickupDayDto? day = null;
                foreach (var d in days)
                {
                    if (d.Date == order.PickupDate)
                    {
                        day = d;
                    }
                }
                if (day == null)
                {
                    day = new PickupDayDto { Date = order.PickupDate };
                    days.Add(day);
                }

                var market = order.Stall!.Market!;
                PickupMarketDto? marketGroup = null;
                foreach (var m in day.Markets)
                {
                    if (m.Market.MarketId == market.MarketId)
                    {
                        marketGroup = m;
                    }
                }
                if (marketGroup == null)
                {
                    marketGroup = new PickupMarketDto { Market = market };
                    day.Markets.Add(marketGroup);
                }

                PickupSlotDto? slot = null;
                foreach (var sl in marketGroup.Slots)
                {
                    if (sl.PickupFrom == order.PickupFrom && sl.PickupTo == order.PickupTo)
                    {
                        slot = sl;
                    }
                }
                if (slot == null)
                {
                    slot = new PickupSlotDto { PickupFrom = order.PickupFrom, PickupTo = order.PickupTo };
                    marketGroup.Slots.Add(slot);
                }

                slot.Orders.Add(order);
                slot.Total = slot.Total + order.TotalAmount;
                marketGroup.Total = marketGroup.Total + order.TotalAmount;
            }

            foreach (var day in days)
            {
                foreach (var marketGroup in day.Markets)
                {
                    foreach (var slot in marketGroup.Slots)
                    {
                        slot.Orders = slot.Orders.OrderBy(o => o.Stall!.StallCode).ToList();
                    }
                }
            }

            return days;
        }

        public async Task<int> CountWaitingOrdersAsync(int customerId)
        {
            return await _context.Orders.CountAsync(o => o.CustomerId == customerId && o.Status == "placed");
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
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    return await TryCancelOrderAsync(customerId, orderId, reason);
                }
                catch (SqlException ex)
                {
                    if (ex.Number != 1205)
                    {
                        throw;
                    }
                    _context.ChangeTracker.Clear();
                }
                catch (DbUpdateException ex)
                {
                    var sqlError = ex.InnerException as SqlException;
                    if (sqlError == null || sqlError.Number != 1205)
                    {
                        throw;
                    }
                    _context.ChangeTracker.Clear();
                }
            }
            return "Many customers are using the system right now. Please try again.";
        }

        private async Task<string> TryCancelOrderAsync(int customerId, int orderId, string reason)
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
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null)
            {
                return "Order not found.";
            }

            string cancelReason = reason.Trim();
            DateTime cancelledAt = DateTime.Now;

            using var transaction = await _context.Database.BeginTransactionAsync();

            int cancelledPlaced = await CancelWithStatusAsync(orderId, "placed", cancelReason, cancelledAt);
            if (cancelledPlaced == 0)
            {
                int cancelledAccepted = await CancelWithStatusAsync(orderId, "accepted", cancelReason, cancelledAt);
                if (cancelledAccepted == 0)
                {
                    return "This order can no longer be cancelled.";
                }

                foreach (var item in order.Items)
                {
                    decimal quantity = item.Quantity;
                    await _context.StockPrices
                        .Where(sp => sp.StockPriceId == item.StockPriceId)
                        .ExecuteUpdateAsync(x => x.SetProperty(sp => sp.QuantityReserved,
                            sp => sp.QuantityReserved - quantity < 0 ? 0 : sp.QuantityReserved - quantity));
                }
            }

            _context.Notifications.Add(new Notification
            {
                UserId = order.Stall!.FarmerId,
                OrderId = order.OrderId,
                Type = "order_cancelled",
                Title = "Order " + order.OrderCode + " was cancelled",
                Body = "Reason: " + cancelReason
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return "";
        }

        private async Task<int> CancelWithStatusAsync(int orderId, string currentStatus, string cancelReason, DateTime cancelledAt)
        {
            return await _context.Orders
                .Where(o => o.OrderId == orderId && o.Status == currentStatus)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(o => o.Status, "cancelled")
                    .SetProperty(o => o.CancelReason, cancelReason)
                    .SetProperty(o => o.CancelledAt, cancelledAt));
        }
    }
}
