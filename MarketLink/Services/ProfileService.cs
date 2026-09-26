using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class ProfileService : IProfileService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IAuthService _authService;

        public ProfileService(MarketLinkDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<CustomerProfile?> GetProfileAsync(int customerId)
        {
            return await _context.CustomerProfiles
                .Include(c => c.User)
                .Include(c => c.District)
                .ThenInclude(d => d!.City)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }

        public async Task<Dictionary<string, string>> ValidateAsync(int customerId, ProfileDto model)
        {
            var errors = new Dictionary<string, string>();

            string phone = _authService.NormalizePhone(model.Phone);
            bool phoneTaken = await _context.Users.AnyAsync(u => u.Phone == phone && u.UserId != customerId);
            if (phoneTaken)
            {
                errors["Phone"] = "This phone number is already used by another account.";
            }

            if (model.DistrictId != null &&
                !await _context.Districts.AnyAsync(d => d.DistrictId == model.DistrictId && d.CityId == model.CityId))
            {
                errors["DistrictId"] = "The district does not belong to the selected city.";
            }

            return errors;
        }

        public async Task<User?> UpdateAsync(int customerId, ProfileDto model)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.CustomerProfile)
                .FirstOrDefaultAsync(u => u.UserId == customerId);

            if (user == null || user.CustomerProfile == null)
            {
                return null;
            }

            user.Phone = _authService.NormalizePhone(model.Phone);
            user.UpdatedAt = DateTime.Now;
            user.CustomerProfile.FullName = model.FullName.Trim();
            user.CustomerProfile.Address = model.Address.Trim();
            user.CustomerProfile.DistrictId = model.DistrictId!.Value;

            await _context.SaveChangesAsync();
            return user;
        }
    }
}
