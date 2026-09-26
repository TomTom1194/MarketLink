using System.Security.Claims;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "customer")]
    public class MyOrdersController : Controller
    {
        private readonly ICustomerOrderService _customerOrderService;
        private readonly IFavoriteService _favoriteService;

        public MyOrdersController(ICustomerOrderService customerOrderService, IFavoriteService favoriteService)
        {
            _customerOrderService = customerOrderService;
            _favoriteService = favoriteService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var orders = await _customerOrderService.GetOrdersAsync(GetCustomerId(), status);
            ViewBag.Status = status;
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var order = await _customerOrderService.GetOrderDetailAsync(GetCustomerId(), id);
            if (order == null)
            {
                return NotFound();
            }

            ViewBag.IsFavorite = await _favoriteService.IsFavoriteAsync(GetCustomerId(), order.Stall!.FarmerId);

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            string error = await _customerOrderService.CancelOrderAsync(GetCustomerId(), id, reason ?? "");

            if (error != "")
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Your order has been cancelled.";
            }

            return RedirectToAction("Detail", new { id = id });
        }

        private int GetCustomerId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
