using System.Security.Claims;
using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Controllers
{
    public class AccountController : Controller
    {
        private readonly MarketLinkDbContext _context;

        public AccountController(MarketLinkDbContext context)
        {
            _context = context;
        }

        // ===================== LOGIN =====================

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Already logged in: go to the home page
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Users can log in with email or phone number
            string login = model.EmailOrPhone.Trim().ToLower();
            string phone = NormalizePhone(login);

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.CustomerProfile)
                .Include(u => u.FarmerProfile)
                .FirstOrDefaultAsync(u => u.Email == login || u.Phone == phone);

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Incorrect email / phone number or password.");
                return View(model);
            }

            if (user.Status != "active")
            {
                ModelState.AddModelError("", "Your account has been disabled. Please contact the administrator.");
                return View(model);
            }

            await SignInUser(user, model.RememberMe);

            // Go back to the page the user was on before being asked to log in
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // ===================== REGISTER (CUSTOMER) =====================

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            LoadCitiesAndDistricts(null, null);
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            string email = model.Email.Trim().ToLower();
            string phone = NormalizePhone(model.Phone);

            // Email and phone number must not be taken
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
            }

            if (await _context.Users.AnyAsync(u => u.Phone == phone))
            {
                ModelState.AddModelError("Phone", "This phone number is already registered.");
            }

            // The district must belong to the selected city
            if (model.DistrictId != null &&
                !await _context.Districts.AnyAsync(d => d.DistrictId == model.DistrictId && d.CityId == model.CityId))
            {
                ModelState.AddModelError("DistrictId", "The district does not belong to the selected city.");
            }

            if (!ModelState.IsValid)
            {
                LoadCitiesAndDistricts(model.CityId, model.DistrictId);
                return View(model);
            }

            var customerRole = await _context.Roles.FirstAsync(r => r.RoleName == "customer");

            // Create the account and the customer profile together.
            // EF sets CustomerId = UserId automatically after inserting the User.
            var user = new User
            {
                RoleId = customerRole.RoleId,
                Email = email,
                Phone = phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Status = "active",
                CustomerProfile = new CustomerProfile
                {
                    FullName = model.FullName.Trim(),
                    Address = model.Address.Trim(),
                    DistrictId = model.DistrictId!.Value
                }
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Log the user in right after registering
            user.Role = customerRole;
            await SignInUser(user, false);

            TempData["Success"] = "Registration successful. Welcome to MarketLink!";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/GetDistricts?cityId=1  (used by the District dropdown on the register form)
        [HttpGet]
        public async Task<IActionResult> GetDistricts(int cityId)
        {
            var districts = await _context.Districts
                .Where(d => d.CityId == cityId)
                .OrderBy(d => d.DistrictName)
                .Select(d => new { d.DistrictId, d.DistrictName })
                .ToListAsync();

            return Json(districts);
        }

        // ===================== LOGOUT =====================

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied  (shown when the user's role is not allowed)
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ===================== HELPERS =====================

        // Store the user's info in the login cookie
        private async Task SignInUser(User user, bool rememberMe)
        {
            string displayName = user.Email;
            if (user.CustomerProfile != null) displayName = user.CustomerProfile.FullName;
            if (user.FarmerProfile != null) displayName = user.FarmerProfile.BrandName;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role!.RoleName)   // customer | farmer | admin
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var properties = new AuthenticationProperties
            {
                IsPersistent = rememberMe   // true: stay logged in after closing the browser
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                properties);
        }

        // Keep digits only: "+84 901 234 567" -> "0901234567"
        private static string NormalizePhone(string input)
        {
            string digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("84") && digits.Length == 11)
            {
                digits = "0" + digits.Substring(2);
            }
            return digits;
        }

        // Load cities and districts for the two dropdowns on the register form
        private void LoadCitiesAndDistricts(int? cityId, int? districtId)
        {
            ViewBag.Cities = new SelectList(
                _context.Cities.OrderBy(c => c.CityName).ToList(),
                "CityId", "CityName", cityId);

            var districts = cityId == null
                ? new List<District>()
                : _context.Districts.Where(d => d.CityId == cityId).OrderBy(d => d.DistrictName).ToList();

            ViewBag.Districts = new SelectList(districts, "DistrictId", "DistrictName", districtId);
        }
    }
}
