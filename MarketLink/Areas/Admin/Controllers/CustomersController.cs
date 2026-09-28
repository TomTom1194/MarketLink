using MarketLink.Dtos;
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
        private const int PageSize = 15;

        public CustomersController(IAdminCustomerService customerService)
        {
            _customerService = customerService;
        }

        // GET: /Admin/Customers?status=active&search=...&page=2
        public async Task<IActionResult> Index(string? status, string? search, int page = 1)
        {
            ViewBag.Status = status;
            ViewBag.Search = search;

            int total = await _customerService.CountCustomersAsync(status, search);
            var pager = PagerDto.Create(page, total, PageSize, "page", "");
            ViewBag.Pager = pager;

            var customers = await _customerService.GetCustomersAsync(status, search, pager);
            return View(customers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id, string? returnStatus, string? returnSearch, int returnPage = 1)
        {
            if (await _customerService.LockAsync(id))
                TempData["Success"] = "The account was locked and the customer was notified by email.";
            else
                TempData["Error"] = "Customer not found.";

            return RedirectToAction(nameof(Index), new { status = returnStatus, search = returnSearch, page = returnPage });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id, string? returnStatus, string? returnSearch, int returnPage = 1)
        {
            string loginUrl = Url.Action("Login", "Account", new { area = "" }, Request.Scheme)!;
            if (await _customerService.UnlockAsync(id, loginUrl))
                TempData["Success"] = "The account was unlocked and the customer was notified by email.";
            else
                TempData["Error"] = "Customer not found.";

            return RedirectToAction(nameof(Index), new { status = returnStatus, search = returnSearch, page = returnPage });
        }
    }
}
