using System.Security.Claims;
using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class FarmersController : Controller
    {
        private readonly IAdminFarmerService _farmerService;

        public FarmersController(IAdminFarmerService farmerService)
        {
            _farmerService = farmerService;
        }

        // GET: /Admin/Farmers?status=pending&search=...
        public async Task<IActionResult> Index(string? status, string? search)
        {
            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.Counts = await _farmerService.CountByStatusAsync();

            var farmers = await _farmerService.GetFarmersAsync(status, search);
            return View(farmers);
        }

        // GET: /Admin/Farmers/Detail/5  (farmer application detail)
        public async Task<IActionResult> Detail(int id)
        {
            var farmer = await _farmerService.GetDetailAsync(id);
            if (farmer == null)
            {
                return NotFound();
            }

            return View(farmer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            int adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Full link to the login page for the email, e.g. https://localhost:7001/Account/Login
            string loginUrl = Url.Action("Login", "Account", new { area = "" }, Request.Scheme)!;

            var result = await _farmerService.ApproveAsync(id, adminId, loginUrl);

            if (!result.Success)
            {
                TempData["Error"] = "This farmer cannot be approved.";
            }
            else if (result.Reactivated)
            {
                TempData["Success"] = "The farmer was reactivated and can sell again with their old password.";
            }
            else if (result.EmailSent)
            {
                TempData["Success"] = $"Approved. The login link and password were emailed to {result.Email}.";
            }
            else
            {
                TempData["Warning"] =
                    $"Approved, but the email to {result.Email} could not be sent ({result.EmailError}). " +
                    $"Please give the farmer this password yourself: {result.TemporaryPassword}";
            }

            return RedirectToAction(nameof(Detail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            if (await _farmerService.RejectAsync(id))
                TempData["Success"] = "The application was rejected and the farmer was notified by email.";
            else
                TempData["Error"] = "Only pending applications can be rejected.";

            return RedirectToAction(nameof(Detail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suspend(int id)
        {
            if (await _farmerService.SuspendAsync(id))
                TempData["Success"] = "The farmer was suspended and can no longer sell.";
            else
                TempData["Error"] = "Only approved farmers can be suspended.";

            return RedirectToAction(nameof(Detail), new { id });
        }
    }
}
