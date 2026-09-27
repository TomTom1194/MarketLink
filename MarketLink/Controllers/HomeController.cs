using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MarketLink.Models;
using MarketLink.Services;

namespace MarketLink.Controllers;

public class HomeController : Controller
{
    private readonly IShopService _shopService;
    private readonly IReviewService _reviewService;

    public HomeController(IShopService shopService, IReviewService reviewService)
    {
        _shopService = shopService;
        _reviewService = reviewService;
    }

    public async Task<IActionResult> Index()
    {
        int? marketId = null;
        string? cookieValue = Request.Cookies["MarketLink.MarketId"];
        int cookieMarketId;
        if (cookieValue != null && int.TryParse(cookieValue, out cookieMarketId))
        {
            marketId = cookieMarketId;
        }

        Market? currentMarket = null;
        if (marketId != null)
        {
            currentMarket = await _shopService.GetMarketAsync(marketId.Value);
            if (currentMarket == null)
            {
                marketId = null;
            }
        }

        ViewBag.CurrentMarket = currentMarket;
        ViewBag.Categories = await _shopService.GetCategoriesAsync();
        ViewBag.Markets = await _shopService.GetActiveMarketsAsync(12);
        var newestProducts = await _shopService.GetNewestProductsAsync(marketId, 12);
        var farmers = await _shopService.GetFeaturedFarmersAsync(6);
        ViewBag.NewestProducts = newestProducts;
        ViewBag.Farmers = farmers;

        var farmerIds = new List<int>();
        foreach (var sp in newestProducts)
        {
            farmerIds.Add(sp.Product!.FarmerId);
        }
        foreach (var f in farmers)
        {
            farmerIds.Add(f.FarmerId);
        }
        ViewBag.FarmerRatings = await _reviewService.GetFarmerRatingsAsync(farmerIds);

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
