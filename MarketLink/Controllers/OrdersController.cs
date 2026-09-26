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

        public async Task<IActionResult> Details(int id)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();
            var order = await _orderService.GetOrderDetailAsync(id, farmerId.Value);
            return order == null ? NotFound() : View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Accept(int id) => RunOrderAction(id, (orderId, farmerId) => _orderService.AcceptOrderAsync(orderId, farmerId), "The order was accepted.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please enter a reason for rejecting the order.";
                return Task.FromResult<IActionResult>(RedirectToAction(nameof(Details), new { id }));
            }

            return RunOrderAction(id, (orderId, farmerId) => _orderService.RejectOrderAsync(orderId, farmerId, reason), "The order was rejected and the customer was notified.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Complete(int id) => RunOrderAction(id, (orderId, farmerId) => _orderService.CompleteOrderAsync(orderId, farmerId), "Pickup confirmed. The order is complete.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> NoShow(int id) => RunOrderAction(id, (orderId, farmerId) => _orderService.MarkNoShowAsync(orderId, farmerId), "The order was marked as no-show and the products are back in stock.");

        private async Task<IActionResult> RunOrderAction(int id, Func<int, int, Task<bool>> action, string successMessage)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();

            try
            {
                if (!await action(id, farmerId.Value))
                {
                    TempData["Error"] = "The order could not be updated. It may already have been handled, or it does not belong to your stall.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = successMessage;
            }
            catch (InvalidOperationException exception)
            {
                TempData["Error"] = exception.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            return RedirectToAction(nameof(Index));
        }

        private int? CurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
        }
    }
}
