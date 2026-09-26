using System.Security.Claims;
using MarketLink.Dtos;
using MarketLink.Models;
using MarketLink.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Controllers
{
    [Authorize(Roles = "customer")]
    public class ProfileController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly IShopService _shopService;
        private readonly IAuthService _authService;

        public ProfileController(IProfileService profileService, IShopService shopService, IAuthService authService)
        {
            _profileService = profileService;
            _shopService = shopService;
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var profile = await _profileService.GetProfileAsync(GetCustomerId());
            if (profile == null)
            {
                return NotFound();
            }

            var model = new ProfileDto
            {
                FullName = profile.FullName,
                Phone = profile.User!.Phone,
                Address = profile.Address,
                DistrictId = profile.DistrictId,
                CityId = profile.District != null ? profile.District.CityId : null
            };

            ViewBag.Email = profile.User.Email;
            await LoadCitiesAndDistricts(model.CityId, model.DistrictId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileDto model)
        {
            int customerId = GetCustomerId();

            var errors = await _profileService.ValidateAsync(customerId, model);
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }

            if (!ModelState.IsValid)
            {
                var profile = await _profileService.GetProfileAsync(customerId);
                ViewBag.Email = profile != null ? profile.User!.Email : "";
                await LoadCitiesAndDistricts(model.CityId, model.DistrictId);
                return View(model);
            }

            User? user = await _profileService.UpdateAsync(customerId, model);
            if (user == null)
            {
                return NotFound();
            }

            var authResult = await HttpContext.AuthenticateAsync();
            bool rememberMe = authResult.Properties != null && authResult.Properties.IsPersistent;
            await _authService.SignInAsync(HttpContext, user, rememberMe);

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction("Index");
        }

        private async Task LoadCitiesAndDistricts(int? cityId, int? districtId)
        {
            var cities = await _shopService.GetCitiesAsync();
            ViewBag.Cities = new SelectList(cities, "CityId", "CityName", cityId);

            var districts = new List<District>();
            if (cityId != null)
            {
                districts = await _shopService.GetDistrictsAsync(cityId.Value);
            }
            ViewBag.Districts = new SelectList(districts, "DistrictId", "DistrictName", districtId);
        }

        private int GetCustomerId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
