using MarketLink.Data;
using MarketLink.Dtos;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Farmer;

public class FarmerDashboardService : IFarmerDashboardService
{
    private readonly MarketLinkDbContext _ctx;

    public FarmerDashboardService(MarketLinkDbContext ctx) => _ctx = ctx;

    public async Task<FarmerDashboardDto> GetDashboardAsync(int farmerId)
    {
        var products = _ctx.Products.AsNoTracking().Where(p => p.FarmerId == farmerId && p.Status != "removed");
        var now = DateTime.Now;
        return new FarmerDashboardDto
        {
            BrandName = await _ctx.FarmerProfiles.AsNoTracking().Where(p => p.FarmerId == farmerId).Select(p => p.BrandName).FirstOrDefaultAsync() ?? "",
            ProductCount = await products.CountAsync(),
            ActiveProductCount = await products.CountAsync(p => p.Status == "active" && p.ExpiresAt > now),
            HiddenProductCount = await products.CountAsync(p => p.Status != "active" || p.ExpiresAt <= now),
            ActiveStallCount = await _ctx.Stalls.AsNoTracking().CountAsync(s => s.FarmerId == farmerId && s.IsActive),
            PendingOrderCount = await _ctx.Orders.AsNoTracking().CountAsync(o => o.Stall!.FarmerId == farmerId && o.Status == "placed"),
            RecentOrders = await _ctx.Orders.AsNoTracking().Where(o => o.Stall!.FarmerId == farmerId)
                .OrderByDescending(o => o.PlacedAt).Take(5)
                .Select(o => new FarmerRecentOrderDto { OrderCode = o.OrderCode, PickupName = o.PickupName, PickupDate = o.PickupDate, Status = o.Status })
                .ToListAsync()
        };
    }

    public async Task<FarmerOrderNotificationsDto> GetOrderNotificationsAsync(int farmerId)
    {
        var pending = _ctx.Orders.AsNoTracking().Where(o => o.Stall!.FarmerId == farmerId && o.Status == "placed");
        return new FarmerOrderNotificationsDto
        {
            Count = await pending.CountAsync(),
            Orders = await pending.OrderByDescending(o => o.PlacedAt).Take(5)
                .Select(o => new FarmerRecentOrderDto { OrderCode = o.OrderCode, PickupName = o.PickupName, PickupDate = o.PickupDate, Status = o.Status })
                .ToListAsync()
        };
    }

    // Products that need a re-up: listing period is over, or the current price row has nothing left to sell.
    // Expired products are already hidden from the shop (the shop only lists products with expires_at in the future).
    public async Task<List<FarmerProductAlertDto>> GetProductAlertsAsync(int farmerId)
    {
        var now = DateTime.Now;
        var products = await _ctx.Products.AsNoTracking()
            .Where(p => p.FarmerId == farmerId && p.Status != "removed")
            .Select(p => new
            {
                p.ProductId,
                p.ProductName,
                p.ExpiresAt,
                Current = p.StockPrices
                    .Where(sp => sp.EffectiveTo == null)
                    .Select(sp => new { Left = sp.QuantityIn - sp.QuantityReserved - sp.QuantitySold, sp.EffectiveFrom })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var alerts = new List<FarmerProductAlertDto>();
        foreach (var p in products)
        {
            if (p.ExpiresAt <= now)
            {
                alerts.Add(new FarmerProductAlertDto { ProductId = p.ProductId, ProductName = p.ProductName, Kind = "expired", Since = p.ExpiresAt });
            }
            else if (p.Current != null && p.Current.Left <= 0)
            {
                alerts.Add(new FarmerProductAlertDto { ProductId = p.ProductId, ProductName = p.ProductName, Kind = "sold_out", Since = p.Current.EffectiveFrom });
            }
        }
        return alerts.OrderByDescending(a => a.Since).ToList();
    }
}
