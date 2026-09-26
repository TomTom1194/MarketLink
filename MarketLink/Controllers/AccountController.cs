using System.Security.Claims;
using MarketLink.Dtos;
using MarketLink.Models;
using MarketLink.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MarketLink.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ICustomerAccountService _customerAccountService;

        public AccountController(IAuthService authService, ICustomerAccountService customerAccountService)
        {
            _authService = authService;
            _customerAccountService = customerAccountService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Already logged in? Send them to the portal of their role
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                string role = User.FindFirstValue(ClaimTypes.Role) ?? "";
                return RedirectToPortal(role);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _authService.CheckLoginAsync(model.EmailOrPhone, model.Password);

            if (user == null)
            {
                ModelState.AddModelError("", "Incorrect email / phone number or password.");
                return View(model);
            }

            if (user.Status != "active")
            {
                ModelState.AddModelError("", "Your account has been disabled. Please contact the administrator.");
                return View(model);
            }

            await _authService.SignInAsync(HttpContext, user, model.RememberMe);

            string role = user.Role!.RoleName;

            // Customers go back to the shop page they came from (e.g. a product page)
            if (role == "customer" && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Admins and farmers always start at their own portal
            return RedirectToPortal(role);
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            // Already logged in? Send them to the portal of their role
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                string role = User.FindFirstValue(ClaimTypes.Role) ?? "";
                return RedirectToPortal(role);
            }

            await LoadCitiesAndDistricts(null, null);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            var errors = await _customerAccountService.ValidateRegisterAsync(model);
            foreach (var error in errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }

            if (!ModelState.IsValid)
            {
                await LoadCitiesAndDistricts(model.CityId, model.DistrictId);
                return View(model);
            }

            var user = await _customerAccountService.RegisterAsync(model);
            await _authService.SignInAsync(HttpContext, user, false);

            TempData["Success"] = "Registration successful. Welcome to MarketLink!";
            return RedirectToAction("Index", "Home");
        }

        // Used by the District dropdown on the register form
        [HttpGet]
        public async Task<IActionResult> GetDistricts(int cityId)
        {
            var districts = await _customerAccountService.GetDistrictsAsync(cityId);
            return Json(districts.Select(d => new { d.DistrictId, d.DistrictName }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _authService.SignOutAsync(HttpContext);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Sends each role to its own start page
        private IActionResult RedirectToPortal(string role)
        {
            if (role == "admin")
            {
                // Admin portal: /Admin
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            if (role == "farmer")
            {
                // Farmer portal: /Orders
                return RedirectToAction("Index", "Orders", new { area = "" });
            }

            // Customer: the shop home page
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        private async Task LoadCitiesAndDistricts(int? cityId, int? districtId)
        {
            var cities = await _customerAccountService.GetCitiesAsync();
            ViewBag.Cities = new SelectList(cities, "CityId", "CityName", cityId);

            var districts = cityId == null
                ? new List<District>()
                : await _customerAccountService.GetDistrictsAsync(cityId.Value);
            ViewBag.Districts = new SelectList(districts, "DistrictId", "DistrictName", districtId);
        }
    }
}
