using MarketLink.Dtos;
using MarketLink.Models;
using MarketLink.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Controllers
{
    // Public page where farmers apply to sell on MarketLink
    public class FarmerApplicationController : Controller
    {
        private readonly IFarmerAccountService _farmerAccountService;

        public FarmerApplicationController(IFarmerAccountService farmerAccountService)
        {
            _farmerAccountService = farmerAccountService;
        }

        // GET: /FarmerApplication
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Already logged in: the Login page sends them to the portal of their role
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new FarmerApplicationDto
            {
                // Usual hours of a morning market; used only if the farmer adds a new market
                NewMarketOpenTime = new TimeSpan(5, 0, 0),
                NewMarketCloseTime = new TimeSpan(11, 0, 0)
            };

            await LoadDropdowns(model);
            return View(model);
        }

        // POST: /FarmerApplication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FarmerApplicationDto model)
        {
            var errors = await _farmerAccountService.ValidateApplicationAsync(model);
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View(model);
            }

            await _farmerAccountService.SubmitApplicationAsync(model);

            TempData["ApplicationEmail"] = model.Email.Trim();
            return RedirectToAction(nameof(Submitted));
        }

        // GET: /FarmerApplication/Submitted
        [HttpGet]
        public IActionResult Submitted()
        {
            ViewBag.Email = TempData["ApplicationEmail"];
            return View();
        }

        private async Task LoadDropdowns(FarmerApplicationDto model)
        {
            var cities = await _farmerAccountService.GetCitiesAsync();
            ViewBag.Cities = new SelectList(cities, "CityId", "CityName", model.CityId);

            // Districts of the city chosen for the farmer's address
            var districts = model.CityId == null
                ? new List<District>()
                : await _farmerAccountService.GetDistrictsAsync(model.CityId.Value);
            ViewBag.Districts = new SelectList(districts, "DistrictId", "DistrictName", model.DistrictId);

            // The market can be in any city, so this list is not filtered by the address
            ViewBag.Markets = await _farmerAccountService.GetActiveMarketsAsync();

            // "Other" market: its own city and district
            ViewBag.MarketCities = new SelectList(cities, "CityId", "CityName", model.NewMarketCityId);

            var marketDistricts = model.NewMarketCityId == null
                ? new List<District>()
                : await _farmerAccountService.GetDistrictsAsync(model.NewMarketCityId.Value);
            ViewBag.MarketDistricts = new SelectList(marketDistricts, "DistrictId", "DistrictName", model.NewMarketDistrictId);
        }
    }
}
