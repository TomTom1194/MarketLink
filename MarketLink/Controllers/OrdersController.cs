using System.Security.Claims;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "farmer")]
    public class OrdersController : Controller
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public async Task<IActionResult> Index(string? phone)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();
            var searchPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            ViewBag.Phone = searchPhone ?? string.Empty;
            return View(await _orderService.GetOrdersAsync(farmerId.Value, searchPhone));
        }

        public async Task<IActionResult> Statistics()
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();
            ViewData["FarmerSection"] = "statistics";
            return View(await _orderService.GetFarmerStatisticsAsync(farmerId.Value));
        }

        public async Task<IActionResult> Details(int id)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();
            var order = await _orderService.GetOrderDetailAsync(id, farmerId.Value);
            return order == null ? NotFound() : View(order);
        }

        public async Task<IActionResult> Process(int id)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();
            var order = await _orderService.GetOrderDetailAsync(id, farmerId.Value);
            return order == null ? NotFound() : View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Accept(int id) => RunOrderAction(id, (orderId, farmerId) => _orderService.AcceptOrderAsync(orderId, farmerId), "Đã xác nhận đơn hàng.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Vui lòng nhập lý do từ chối đơn hàng.";
                return Task.FromResult<IActionResult>(RedirectToAction(nameof(Process), new { id }));
            }

            return RunOrderAction(id, (orderId, farmerId) => _orderService.RejectOrderAsync(orderId, farmerId, reason), "Đã từ chối đơn hàng và thông báo cho khách hàng.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Complete(int id) => RunOrderAction(id, (orderId, farmerId) => _orderService.CompleteOrderAsync(orderId, farmerId), "Đã xác nhận khách nhận hàng và hoàn tất đơn.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Cancel(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            {
                TempData["Error"] = "Vui lòng nhập lý do hủy đơn (tối đa 500 ký tự).";
                return Task.FromResult<IActionResult>(RedirectToAction(nameof(Process), new { id }));
            }

            return RunOrderAction(id, (orderId, farmerId) => _orderService.CancelFarmerOrderAsync(orderId, farmerId, reason), "Đã hủy đơn hàng và thông báo cho khách hàng.");
        }

        private async Task<IActionResult> RunOrderAction(int id, Func<int, int, Task<bool>> action, string successMessage)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();

            try
            {
                if (!await action(id, farmerId.Value))
                {
                    TempData["Error"] = "Không thể cập nhật đơn hàng. Đơn có thể đã được xử lý hoặc không thuộc gian hàng của bạn.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = successMessage;
            }
            catch (InvalidOperationException exception)
            {
                TempData["Error"] = exception.Message;
                return RedirectToAction(nameof(Process), new { id });
            }

            return RedirectToAction(nameof(Index));
        }

        private int? CurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
        }
    }
}
