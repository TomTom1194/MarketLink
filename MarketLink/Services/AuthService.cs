using System.Security.Claims;
using MarketLink.Data;
using MarketLink.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    // Account logic shared by every role: customer, farmer, admin
    public class AuthService : IAuthService
    {
        private readonly MarketLinkDbContext _context;

        public AuthService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public string NormalizeEmail(string email)
        {
            return email.Trim().ToLower();
        }

        // "+84 901 234 567" -> "0901234567"
        public string NormalizePhone(string phone)
        {
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("84") && digits.Length == 11)
            {
                digits = "0" + digits.Substring(2);
            }
            return digits;
        }

        public async Task<bool> IsEmailTakenAsync(string email)
        {
            string normalized = NormalizeEmail(email);
            return await _context.Users.AnyAsync(u => u.Email == normalized);
        }

        public async Task<bool> IsPhoneTakenAsync(string phone)
        {
            string normalized = NormalizePhone(phone);
            return await _context.Users.AnyAsync(u => u.Phone == normalized);
        }

        // Builds a new user with a hashed password. The caller adds a profile and saves it.
        public async Task<User> CreateUserAsync(string roleName, string email, string phone, string password)
        {
            var role = await _context.Roles.FirstAsync(r => r.RoleName == roleName);

            return new User
            {
                RoleId = role.RoleId,
                Role = role,
                Email = NormalizeEmail(email),
                Phone = NormalizePhone(phone),
                PasswordHash = HashPassword(password),
                Status = "active"
            };
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // Returns the user when the email/phone and password are correct, otherwise null.
        // The caller still checks user.Status.
        public async Task<User?> CheckLoginAsync(string emailOrPhone, string password)
        {
            string email = NormalizeEmail(emailOrPhone);
            string phone = NormalizePhone(emailOrPhone);

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.CustomerProfile)
                .Include(u => u.FarmerProfile)
                .FirstOrDefaultAsync(u => u.Email == email || (phone != "" && u.Phone == phone));

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return null;
            }

            return user;
        }

        public async Task SignInAsync(HttpContext httpContext, User user, bool rememberMe)
        {
            string displayName = user.Email;
            if (user.CustomerProfile != null) displayName = user.CustomerProfile.FullName;
            if (user.FarmerProfile != null) displayName = user.FarmerProfile.BrandName;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role!.RoleName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var properties = new AuthenticationProperties
            {
                IsPersistent = rememberMe
            };

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                properties);
        }

        public async Task SignOutAsync(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
