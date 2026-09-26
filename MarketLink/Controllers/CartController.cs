using System.Security.Claims;
using MarketLink.Dtos;
using MarketLink.Helpers;
using MarketLink.Models;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "customer")]
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly ICheckoutService _checkoutService;

        public CartController(ICartService cartService, ICheckoutService checkoutService)
        {
            _cartService = cartService;
            _checkoutService = checkoutService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? marketId)
        {
            int customerId = GetCustomerId();

            var allCarts = await _cartService.GetAllCartsAsync(customerId);
            ViewBag.AllCarts = allCarts;

            if (marketId == null)
            {
                string? cookieValue = Request.Cookies["MarketLink.MarketId"];
                int cookieMarketId;
                if (cookieValue != null && int.TryParse(cookieValue, out cookieMarketId))
                {
                    marketId = cookieMarketId;
                }
            }

            if (marketId == null && allCarts.Count > 0)
            {
                marketId = allCarts[0].MarketId;
            }

            if (marketId == null)
            {
                return View();
            }

            List<string> cartNotices = await _cartService.RefreshCartAsync(customerId, marketId.Value);
            string? reorderNotices = TempData["ReorderNotices"] as string;
            if (!string.IsNullOrEmpty(reorderNotices))
            {
                cartNotices.InsertRange(0, reorderNotices.Split('\n'));
            }
            ViewBag.CartNotices = cartNotices;

            var cart = await _cartService.GetCartAsync(customerId, marketId.Value);

            if ((cart == null || cart.Items.Count == 0) && allCarts.Count > 0)
            {
                return RedirectToAction("Index", new { marketId = allCarts[0].MarketId });
            }

            var commonDates = new List<DateTime>();
            if (cart != null)
            {
                var stalls = new List<Stall>();
                foreach (var item in cart.Items)
                {
                    var stall = item.StockPrice!.Stall!;
                    if (ShopHelper.ItemStatus(item.StockPrice) == "ok" && !stalls.Any(s => s.StallId == stall.StallId))
                    {
                        stalls.Add(stall);
                    }
                }
                commonDates = _checkoutService.GetCommonPickupDates(stalls, cart.Market!);
            }
            ViewBag.CommonDates = commonDates;

            var customer = await _checkoutService.GetCustomerAsync(customerId);
            var checkoutForm = new CheckoutDto { MarketId = marketId.Value };
            if (customer != null)
            {
                checkoutForm.PickupName = customer.FullName;
                checkoutForm.PickupPhone = customer.User!.Phone;
            }
            ViewBag.CheckoutForm = checkoutForm;

            return View(cart);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int stockPriceId, decimal quantity, string? returnUrl)
        {
            string productPage = Url.Action("Detail", "Shop", new { id = stockPriceId })!;

            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                TempData["Error"] = "Please log in to add products to your basket.";
                return RedirectToAction("Login", "Account", new { returnUrl = productPage });
            }

            if (!User.IsInRole("customer"))
            {
                TempData["Error"] = "Only customer accounts can buy products.";
                return Redirect(productPage);
            }

            string error = await _cartService.AddToCartAsync(GetCustomerId(), stockPriceId, quantity);

            if (error != "")
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Added to your basket.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect(productPage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, decimal quantity, int marketId)
        {
            string error = await _cartService.UpdateQuantityAsync(GetCustomerId(), cartItemId, quantity);

            if (error != "")
            {
                TempData["Error"] = error;
            }

            return RedirectToAction("Index", new { marketId = marketId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int cartItemId, int marketId)
        {
            bool removed = await _cartService.RemoveItemAsync(GetCustomerId(), cartItemId);

            if (!removed)
            {
                TempData["Error"] = "Item not found in your basket.";
            }

            return RedirectToAction("Index", new { marketId = marketId });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Count()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("customer"))
            {
                return Json(new { count = 0 });
            }

            int itemCount = await _cartService.CountItemsAsync(GetCustomerId());
            return Json(new { count = itemCount });
        }

        private int GetCustomerId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
