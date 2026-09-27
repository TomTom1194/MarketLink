using System.Security.Claims;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class DisputesController : Controller
    {
        private readonly IDisputeService _disputeService;

        public DisputesController(IDisputeService disputeService)
        {
            _disputeService = disputeService;
        }

        public async Task<IActionResult> Index(string? status)
        {
            if (status != "resolved")
            {
                status = "open";
            }

            ViewBag.Status = status;
            var disputes = await _disputeService.GetDisputesAsync(status);
            return View(disputes);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var dispute = await _disputeService.GetDisputeDetailAsync(id);
            if (dispute == null || dispute.Order == null)
            {
                return NotFound();
            }

            ViewBag.CustomerWarnings = await _disputeService.CountWarningsAsync(dispute.Order.CustomerId);
            ViewBag.FarmerWarnings = await _disputeService.CountWarningsAsync(dispute.Order.Stall!.FarmerId);
            ViewBag.CanResolve = _disputeService.CanResolve(dispute);
            return View(dispute);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, string? atFault, string? adminNote)
        {
            int adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            string error = await _disputeService.ResolveAsync(id, adminId, atFault, adminNote);

            if (error != "")
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Detail), new { id });
            }

            TempData["Success"] = "The case was closed. The order was cancelled and both sides were notified.";
            return RedirectToAction(nameof(Detail), new { id });
        }
    }
}
