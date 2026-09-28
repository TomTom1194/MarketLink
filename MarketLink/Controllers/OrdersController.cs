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
        private readonly IDisputeService _disputeService;

        public OrdersController(IOrderService orderService, IDisputeService disputeService)
        {
            _orderService = orderService;
            _disputeService = disputeService;
        }

        public async Task<IActionResult> Index(string? phone)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();
            var searchPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            ViewBag.Phone = searchPhone ?? string.Empty;
            ViewBag.AutoAcceptEnabled = await _orderService.GetAutoAcceptEnabledAsync(farmerId.Value);
            return View(await _orderService.GetOrdersAsync(farmerId.Value, searchPhone));
        }

        // Only the signed-in farmer can change the switch for their active stall.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAutoAccept(bool enabled)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();

            if (!await _orderService.SetAutoAcceptEnabledAsync(farmerId.Value, enabled))
                TempData["Error"] = "No active stall was found for your account.";
            else
                TempData["Success"] = enabled ? "Automatic order acceptance is on for new orders." : "Automatic order acceptance is off.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();
            var order = await _orderService.GetOrderDetailAsync(id, farmerId.Value);
            if (order == null)
            {
                return NotFound();
            }

            ViewBag.Dispute = await _disputeService.GetByOrderAsync(order.Id);
            ViewBag.CanReport = order.Status == "accepted" && DateTime.Now >= order.PickupEnd && DateTime.Now <= order.PickupEnd.AddHours(AutoCancelService.WaitHours);
            return View(order);
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
        public async Task<IActionResult> Report(int id, string? reason)
        {
            var farmerId = CurrentUserId();
            if (farmerId == null) return Forbid();

            string error = await _disputeService.SendFarmerReasonAsync(farmerId.Value, id, reason);
            if (error != "")
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Thanks, we received your side. MarketLink will review this order and let you know the result.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

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
