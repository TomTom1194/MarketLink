using MarketLink.Data;
using MarketLink.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    // Read-only numbers for the dashboard and the reports page
    public class AdminReportService : IAdminReportService
    {
        private readonly MarketLinkDbContext _context;

        public AdminReportService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboardAsync()
        {
            DateTime today = DateTime.Today;
            DateTime firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            var dashboard = new DashboardDto
            {
                ApprovedFarmers = await _context.FarmerProfiles.CountAsync(f => f.ApprovalStatus == "approved"),
                PendingFarmers = await _context.FarmerProfiles.CountAsync(f => f.ApprovalStatus == "pending"),
                ActiveCustomers = await _context.CustomerProfiles.CountAsync(c => c.User!.Status == "active"),
                ActiveMarkets = await _context.Markets.CountAsync(m => m.IsActive),
                TotalMarkets = await _context.Markets.CountAsync(),
                TotalOrders = await _context.Orders.CountAsync(),
                OrdersToday = await _context.Orders.CountAsync(o => o.PlacedAt >= today),
                RevenueThisMonth = await _context.Orders
                    .Where(o => o.Status == "completed" && o.CompletedAt >= firstDayOfMonth)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0
            };

            dashboard.LatestPendingFarmers = await _context.FarmerProfiles
                .Where(f => f.ApprovalStatus == "pending")
                .OrderByDescending(f => f.CreatedAt)
                .Take(5)
                .Select(f => new FarmerRowDto
                {
                    FarmerId = f.FarmerId,
                    BrandName = f.BrandName,
                    ContactPerson = f.ContactPerson,
                    Email = f.User!.Email,
                    Phone = f.User.Phone,
                    DistrictName = f.District!.DistrictName,
                    CityName = f.District.City!.CityName,
                    ApprovalStatus = f.ApprovalStatus,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();

            return dashboard;
        }

        public async Task<ReportDto> GetReportAsync(DateTime from, DateTime to)
        {
            DateTime start = from.Date;
            DateTime end = to.Date.AddDays(1);   // include the whole "to" day

            // Revenue = completed orders only, because customers pay at the stall
            var completed = _context.Orders
                .Where(o => o.Status == "completed" && o.CompletedAt >= start && o.CompletedAt < end);

            var report = new ReportDto
            {
                From = start,
                To = to.Date,
                CompletedOrders = await completed.CountAsync(),
                TotalRevenue = await completed.SumAsync(o => (decimal?)o.TotalAmount) ?? 0
            };

            report.OrdersByStatus = await _context.Orders
                .Where(o => o.PlacedAt >= start && o.PlacedAt < end)
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            report.RevenueByMarket = await completed
                .GroupBy(o => new { o.Stall!.MarketId, o.Stall.Market!.MarketName })
                .Select(g => new MarketRevenueRow
                {
                    MarketName = g.Key.MarketName,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync();
            report.RevenueByMarket = report.RevenueByMarket.OrderByDescending(r => r.Revenue).ToList();

            report.TopFarmers = await completed
                .GroupBy(o => new { o.Stall!.FarmerId, o.Stall.Farmer!.BrandName })
                .Select(g => new FarmerRevenueRow
                {
                    BrandName = g.Key.BrandName,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync();
            report.TopFarmers = report.TopFarmers.OrderByDescending(r => r.Revenue).Take(10).ToList();

            return report;
        }
    }
}
