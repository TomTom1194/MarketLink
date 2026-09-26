using MarketLink.Dtos;
using MarketLink.Models;
using MarketLink.Services;
using MarketLink.Services.Farmer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;


namespace MarketLink.Controllers
{
    [Authorize(Roles = "farmer")]
    public class FarmerController : Controller
    {
        private readonly IFarmerProfileService _fps;
        private readonly ICustomerAccountService _cas;
        private readonly IFarmerStallManagementService _stallService;
        private readonly IFarmerDashboardService _dashboardService;

        public FarmerController(
            IFarmerProfileService farmerProfileService,
            ICustomerAccountService customerAccountService,
            IFarmerStallManagementService stallService,
            IFarmerDashboardService dashboardService)
        {
            _fps = farmerProfileService;
            _cas = customerAccountService;
            _stallService = stallService;
            _dashboardService = dashboardService;
        }
        public async Task<IActionResult> Index()
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            return View(await _dashboardService.GetDashboardAsync(farmerId.Value));
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var farmerId = GetFarmerId();
            if (farmerId == null)
            {
                return Challenge();
            }
            var profile = await _fps.GetFarmerProfileAsync(farmerId.Value);
            if (profile == null)
            {
                return NotFound();
            }

            var model = new UpdateFarmerProfileDto
            {
                BrandName = profile.BrandName,
                ContactPerson = profile.ContactPerson,
                CityId = profile.District?.CityId,
                DistrictId = profile.DistrictId,
                Address = profile.Address,
                Description = profile.Description
            };
            ViewBag.ApprovalStatus = profile.ApprovalStatus;
            await LoadCitiesAndDistrictsAsync(model.CityId,model.DistrictId);
            return View(model);
        }

        private int? GetFarmerId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(id, out var farmerId)? farmerId : null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UpdateFarmerProfileDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null)
            {
                return Challenge();
            }

            if (ModelState.IsValid)
            {
                var errors = await _fps.ValidateUpdateAsync(
                    farmerId.Value,
                    model);

                foreach (var error in errors)
                {
                    ModelState.AddModelError(error.Key, error.Value);
                }
            }

            if (!ModelState.IsValid)
            {
                var profile = await _fps.GetFarmerProfileAsync(farmerId.Value);
                ViewBag.ApprovalStatus = profile?.ApprovalStatus;
                await LoadCitiesAndDistrictsAsync(model.CityId, model.DistrictId);
                return View(model);
            }

            await _fps.UpdateProfileAsync(farmerId.Value, model);

            TempData["Success"] = "Farm profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }
        public async Task<IActionResult> Stalls()
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();

            var stalls = await _stallService.GetStallsAsync(farmerId.Value);
            return View(stalls);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSellingDays(int stallId, UpdateStallSellingDaysDto model)
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();

            if (ModelState.IsValid)
            {
                var days = model.SellingDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (days.Distinct().Count() != days.Length)
                    ModelState.AddModelError(nameof(model.SellingDays), "Selling days cannot be repeated.");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values.SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage).FirstOrDefault() ?? "Invalid selling days.";
                return RedirectToAction(nameof(Stalls));
            }

            var updated = await _stallService.UpdateSellingDaysAsync(farmerId.Value, stallId, model);
            if (updated == null)
                TempData["Error"] = "No stall belongs to your account, or the selling days are invalid.";
            else
                TempData["Success"] = "Stall selling days updated.";

            return RedirectToAction(nameof(Stalls));
        }

        [HttpGet]
        public IActionResult Orders()
        {
            return RedirectToAction("Index", "Orders");
        }

        [HttpGet]
        public async Task<IActionResult> OrderNotifications()
        {
            var farmerId = GetFarmerId();
            if (farmerId == null) return Challenge();
            return Json(await _dashboardService.GetOrderNotificationsAsync(farmerId.Value));
        }

        private async Task LoadCitiesAndDistrictsAsync(int? cityId, int? districtId)
        {
            var cities = await _cas.GetCitiesAsync();
            ViewBag.Cities = new SelectList(
                cities,
                "CityId",
                "CityName",
                cityId);

            var districts = cityId.HasValue
                ? await _cas.GetDistrictsAsync(cityId.Value)
                : new List<District>();

            ViewBag.Districts = new SelectList(
                districts,
                "DistrictId",
                "DistrictName",
                districtId);
        }
    }
}
