using MarketLink.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class CustomersController : Controller
    {
        private readonly IAdminCustomerService _customerService;

        public CustomersController(IAdminCustomerService customerService)
        {
            _customerService = customerService;
        }

        // GET: /Admin/Customers?status=active&search=...
        public async Task<IActionResult> Index(string? status, string? search)
        {
            ViewBag.Status = status;
            ViewBag.Search = search;

            var customers = await _customerService.GetCustomersAsync(status, search);
            return View(customers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id, string? returnStatus)
        {
            if (await _customerService.LockAsync(id))
                TempData["Success"] = "The account was locked and the customer was notified by email.";
            else
                TempData["Error"] = "Customer not found.";

            return RedirectToAction(nameof(Index), new { status = returnStatus });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id, string? returnStatus)
        {
            string loginUrl = Url.Action("Login", "Account", new { area = "" }, Request.Scheme)!;
            if (await _customerService.UnlockAsync(id, loginUrl))
                TempData["Success"] = "The account was unlocked and the customer was notified by email.";
            else
                TempData["Error"] = "Customer not found.";

            return RedirectToAction(nameof(Index), new { status = returnStatus });
        }
    }
}
