using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class DashboardController : Controller
    {
        private readonly IAdminReportService _reportService;

        public DashboardController(IAdminReportService reportService)
        {
            _reportService = reportService;
        }

        // GET: /Admin  or  /Admin/Dashboard
        public async Task<IActionResult> Index()
        {
            var dashboard = await _reportService.GetDashboardAsync();
            return View(dashboard);
        }
    }
}
