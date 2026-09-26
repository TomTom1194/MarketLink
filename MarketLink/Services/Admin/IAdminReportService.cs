using MarketLink.Dtos.Admin;

namespace MarketLink.Services.Admin
{
    public interface IAdminReportService
    {
        Task<DashboardDto> GetDashboardAsync();

        // from / to are dates; both days are included
        Task<ReportDto> GetReportAsync(DateTime from, DateTime to);
    }
}
