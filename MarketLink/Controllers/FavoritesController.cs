using System.Security.Claims;
using MarketLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "customer")]
    public class FavoritesController : Controller
    {
        private readonly IFavoriteService _favoriteService;

        public FavoritesController(IFavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var favorites = await _favoriteService.GetFavoriteFarmersAsync(GetCustomerId());

            var farmerIds = new List<int>();
            foreach (var f in favorites)
            {
                farmerIds.Add(f.FarmerId);
            }
            ViewBag.ProductCounts = await _favoriteService.CountProductsOnSaleAsync(farmerIds);

            return View(favorites);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int farmerId, string? returnUrl)
        {
            string error = await _favoriteService.AddFavoriteAsync(GetCustomerId(), farmerId);

            if (error != "")
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Farmer saved to your favorites.";
            }

            return GoBack(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int farmerId, string? returnUrl)
        {
            bool removed = await _favoriteService.RemoveFavoriteAsync(GetCustomerId(), farmerId);

            if (removed)
            {
                TempData["Success"] = "Farmer removed from your favorites.";
            }

            return GoBack(returnUrl);
        }

        private IActionResult GoBack(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index");
        }

        private int GetCustomerId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
