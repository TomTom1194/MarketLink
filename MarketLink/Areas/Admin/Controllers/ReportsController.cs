using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ReportsController : Controller
    {
        private readonly IAdminReportService _reportService;

        public ReportsController(IAdminReportService reportService)
        {
            _reportService = reportService;
        }

        // GET: /Admin/Reports?from=2026-09-01&to=2026-09-30
        // Default: the last 30 days
        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            DateTime end = (to ?? DateTime.Today).Date;
            DateTime start = (from ?? end.AddDays(-29)).Date;

            if (start > end)
            {
                (start, end) = (end, start);
            }

            var report = await _reportService.GetReportAsync(start, end);
            return View(report);
        }
    }
}
