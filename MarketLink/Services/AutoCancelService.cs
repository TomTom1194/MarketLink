using MarketLink.Data;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class AutoCancelService : BackgroundService
    {
        public const int WaitHours = 48;
        public const string CancelReason = "Not picked up and not reported within 2 days after the pickup time.";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoCancelService> _logger;

        public AutoCancelService(IServiceScopeFactory scopeFactory, ILogger<AutoCancelService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int count = await CancelOldOrdersAsync();
                    if (count > 0)
                    {
                        _logger.LogInformation("Auto-cancelled {Count} order(s) that were not picked up.", count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-cancel of old orders failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public async Task<int> CancelOldOrdersAsync()
        {
            DateTime now = DateTime.Now;
            DateTime lastPickupDate = now.AddHours(-WaitHours).Date;

            var orderIds = new List<int>();
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<MarketLinkDbContext>();
                orderIds = await context.Orders
                    .Where(o => o.Status == "accepted" && o.PickupDate <= lastPickupDate)
                    .Select(o => o.OrderId)
                    .ToListAsync();
            }

            int count = 0;
            foreach (int orderId in orderIds)
            {
                try
                {
                    bool cancelled = await CancelOneOrderAsync(orderId, now);
                    if (cancelled)
                    {
                        count = count + 1;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not auto-cancel order {OrderId}.", orderId);
                }
            }
            return count;
        }

        private async Task<bool> CancelOneOrderAsync(int orderId, DateTime now)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MarketLinkDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var order = await context.Orders
                .Include(o => o.Items)
                .Include(o => o.Stall)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null || order.Status != "accepted")
            {
                return false;
            }

            DateTime pickupEnd = order.PickupDate.Date.Add(order.PickupTo);
            if (now < pickupEnd.AddHours(WaitHours))
            {
                return false;
            }

            bool hasDispute = await context.OrderDisputes.AnyAsync(d => d.OrderId == order.OrderId);
            if (hasDispute)
            {
                return false;
            }

            order.Status = "cancelled";
            order.CancelReason = CancelReason;
            order.CancelledAt = now;

            foreach (var item in order.Items)
            {
                var stock = await context.StockPrices.FirstOrDefaultAsync(sp => sp.StockPriceId == item.StockPriceId);
                if (stock != null)
                {
                    stock.QuantityReserved = Math.Max(0, stock.QuantityReserved - item.Quantity);
                }
            }

            string title = "Order #" + order.OrderCode + " was closed";
            string body = "Nobody confirmed the pickup or reported a problem within 2 days, so the order was cancelled.";
            context.Notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                OrderId = order.OrderId,
                Type = "order_cancelled",
                Title = title,
                Body = body,
                IsRead = false,
                CreatedAt = now
            });
            context.Notifications.Add(new Notification
            {
                UserId = order.Stall!.FarmerId,
                OrderId = order.OrderId,
                Type = "order_cancelled",
                Title = title,
                Body = body + " The reserved quantity is back on sale.",
                IsRead = false,
                CreatedAt = now
            });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
    }
}
