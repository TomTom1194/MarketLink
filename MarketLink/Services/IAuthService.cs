using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IAuthService
    {
        string NormalizeEmail(string email);

        string NormalizePhone(string phone);

        Task<bool> IsEmailTakenAsync(string email);

        Task<bool> IsPhoneTakenAsync(string phone);

        Task<User> CreateUserAsync(string roleName, string email, string phone, string password);

        // BCrypt hash of a password, e.g. when the admin approves a farmer
        string HashPassword(string password);

        Task<User?> CheckLoginAsync(string emailOrPhone, string password);

        Task SignInAsync(HttpContext httpContext, User user, bool rememberMe);

        Task SignOutAsync(HttpContext httpContext);
    }
}
