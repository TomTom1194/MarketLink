using System.Security.Claims;
using MarketLink.Helpers;
using MarketLink.Models;
using MarketLink.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Controllers
{
    public class ShopController : Controller
    {
        private readonly IShopService _shopService;
        private readonly ICheckoutService _checkoutService;
        private readonly IFavoriteService _favoriteService;

        public ShopController(IShopService shopService, ICheckoutService checkoutService, IFavoriteService favoriteService)
        {
            _shopService = shopService;
            _checkoutService = checkoutService;
            _favoriteService = favoriteService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? cityId, int? districtId, double? lat, double? lng)
        {
            if (lat != null && lng != null && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180)
            {
                ViewBag.NearestMarkets = await _shopService.GetNearestMarketsAsync(lat.Value, lng.Value);
            }
            else
            {
                ViewBag.AllMarkets = await _shopService.GetActiveMarketsAsync(50);
            }

            var cities = await _shopService.GetCitiesAsync();
            ViewBag.Cities = new SelectList(cities, "CityId", "CityName", cityId);

            var districts = new List<District>();
            if (cityId != null)
            {
                districts = await _shopService.GetDistrictsAsync(cityId.Value);
            }
            ViewBag.Districts = new SelectList(districts, "DistrictId", "DistrictName", districtId);

            var markets = new List<Market>();
            if (districtId != null)
            {
                markets = await _shopService.GetMarketsAsync(districtId.Value);
            }

            ViewBag.CityId = cityId;
            ViewBag.DistrictId = districtId;
            ViewBag.SelectedMarketId = GetSelectedMarketId();

            return View(markets);
        }

        [HttpGet]
        public async Task<IActionResult> GetDistricts(int cityId)
        {
            var districts = await _shopService.GetDistrictsAsync(cityId);
            return Json(districts.Select(d => new { d.DistrictId, d.DistrictName }));
        }

        [HttpGet]
        public async Task<IActionResult> GetMarkets(int districtId)
        {
            var markets = await _shopService.GetMarketsAsync(districtId);
            return Json(markets.Select(m => new
            {
                m.MarketId,
                m.MarketName,
                m.MapUrl,
                Address = ShopHelper.AddressFromMapUrl(m.MapUrl),
                m.ImageUrl,
                m.OpenDays,
                OpenTime = m.OpenTime.ToString(@"hh\:mm"),
                CloseTime = m.CloseTime.ToString(@"hh\:mm")
            }));
        }

        [HttpGet]
        public async Task<IActionResult> SelectMarket(int marketId)
        {
            var market = await _shopService.GetMarketAsync(marketId);
            if (market == null)
            {
                TempData["Error"] = "This market does not exist or is closed.";
                return RedirectToAction("Index");
            }

            Response.Cookies.Append("MarketLink.MarketId", marketId.ToString(), new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(30),
                HttpOnly = true
            });

            return RedirectToAction("Products", new { marketId = marketId });
        }

        [HttpGet]
        public async Task<IActionResult> Products(int? marketId, string? keyword, int? categoryId, string? sort)
        {
            if (marketId == null)
            {
                marketId = GetSelectedMarketId();
            }

            if (marketId == null)
            {
                return RedirectToAction("Index");
            }

            var market = await _shopService.GetMarketAsync(marketId.Value);
            if (market == null)
            {
                Response.Cookies.Delete("MarketLink.MarketId");
                TempData["Error"] = "This market does not exist or is closed.";
                return RedirectToAction("Index");
            }

            var products = await _shopService.SearchProductsAsync(marketId.Value, keyword, categoryId, sort);

            ViewBag.Market = market;
            ViewBag.Categories = await _shopService.GetCategoriesAsync();
            ViewBag.Keyword = keyword;
            ViewBag.CategoryId = categoryId;
            ViewBag.Sort = sort;

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var product = await _shopService.GetProductDetailAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewBag.OtherProducts = await _shopService.GetOtherProductsOfStallAsync(product.StallId, product.StockPriceId);
            ViewBag.PickupDates = _checkoutService.GetPickupDates(product.Stall!, product.Stall!.Market!);

            bool isFavorite = false;
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("customer"))
            {
                int customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                isFavorite = await _favoriteService.IsFavoriteAsync(customerId, product.Stall.FarmerId);
            }
            ViewBag.IsFavorite = isFavorite;

            return View(product);
        }

        private int? GetSelectedMarketId()
        {
            string? cookieValue = Request.Cookies["MarketLink.MarketId"];
            int marketId;
            if (cookieValue != null && int.TryParse(cookieValue, out marketId))
            {
                return marketId;
            }
            return null;
        }
    }
}
