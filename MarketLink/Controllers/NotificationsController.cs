using System.Security.Claims;
using MarketLink.Services;
using MarketLink.Services.Farmer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "farmer,customer")]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly IFarmerDashboardService _dashboardService;

        public NotificationsController(INotificationService notificationService, IFarmerDashboardService dashboardService)
        {
            _notificationService = notificationService;
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            if (!TryGetUserId(out var userId)) return Forbid();
            var notifications = await _notificationService.GetForUserAsync(userId);
            ViewBag.UnreadCount = notifications.Count(notification => !notification.IsRead);

            // Farmers: products that sold out or expired and need a re-up
            ViewBag.ProductAlerts = User.IsInRole("farmer")
                ? await _dashboardService.GetProductAlertsAsync(userId)
                : new List<MarketLink.Dtos.FarmerProductAlertDto>();
            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            if (!TryGetUserId(out var userId)) return Forbid();
            await _notificationService.MarkAllReadAsync(userId);
            return RedirectToAction(nameof(Index));
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
