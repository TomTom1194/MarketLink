using MarketLink.Data;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    // Checks newly placed orders while the farmer's auto-accept switch is on.
    public class AutoAcceptOrderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoAcceptOrderService> _logger;

        public AutoAcceptOrderService(IServiceScopeFactory scopeFactory, ILogger<AutoAcceptOrderService> logger)
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
                    await ProcessDueOrdersAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Automatic order acceptance failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessDueOrdersAsync(CancellationToken cancellationToken)
        {
            List<int> orderIds;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<MarketLinkDbContext>();
                var cutoff = DateTime.Now - OrderService.AutoAcceptDelay;
                orderIds = await context.Orders.AsNoTracking()
                    .Where(order => order.Status == "placed" && order.AutoAcceptFailedAt == null &&
                        order.PlacedAt <= cutoff && order.Stall != null && order.Stall.IsActive &&
                        order.Stall.Farmer != null && order.Stall.Farmer.ApprovalStatus == "approved" && order.Stall.AutoAcceptEnabled &&
                        order.Stall.AutoAcceptEnabledAt != null && order.PlacedAt >= order.Stall.AutoAcceptEnabledAt)
                    .OrderBy(order => order.PlacedAt)
                    .Select(order => order.OrderId)
                    .Take(100)
                    .ToListAsync(cancellationToken);
            }

            foreach (var orderId in orderIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                    await orderService.AutoAcceptOrderAsync(orderId);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Could not auto-accept order {OrderId}; it will be retried.", orderId);
                }
            }
        }
    }
}
