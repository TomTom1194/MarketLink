using System.Security.Claims;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "farmer,customer")]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            if (!TryGetUserId(out var userId)) return Forbid();
            var notifications = await _notificationService.GetForUserAsync(userId);
            ViewBag.UnreadCount = notifications.Count(notification => !notification.IsRead);
            if (User.IsInRole("farmer")) ViewData["FarmerSection"] = "notifications";
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
