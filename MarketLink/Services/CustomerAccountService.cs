using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    // Customer sign-up: customers register on the website and can shop right away
    public class CustomerAccountService : ICustomerAccountService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IAuthService _authService;

        public CustomerAccountService(MarketLinkDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // Returns errors keyed by form field name. An empty list means the data is valid.
        public async Task<Dictionary<string, string>> ValidateRegisterAsync(RegisterDto model)
        {
            var errors = new Dictionary<string, string>();

            if (await _authService.IsEmailTakenAsync(model.Email))
            {
                errors["Email"] = "This email is already registered.";
            }

            if (await _authService.IsPhoneTakenAsync(model.Phone))
            {
                errors["Phone"] = "This phone number is already registered.";
            }

            if (model.DistrictId != null &&
                !await _context.Districts.AnyAsync(d => d.DistrictId == model.DistrictId && d.CityId == model.CityId))
            {
                errors["DistrictId"] = "The district does not belong to the selected city.";
            }

            return errors;
        }

        // Creates the account and the customer profile in one save.
        // EF sets CustomerId = UserId after inserting the user.
        public async Task<User> RegisterAsync(RegisterDto model)
        {
            var user = await _authService.CreateUserAsync("customer", model.Email, model.Phone, model.Password);

            user.CustomerProfile = new CustomerProfile
            {
                FullName = model.FullName.Trim(),
                Address = model.Address.Trim(),
                DistrictId = model.DistrictId!.Value
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<List<City>> GetCitiesAsync()
        {
            return await _context.Cities.OrderBy(c => c.CityName).ToListAsync();
        }

        public async Task<List<District>> GetDistrictsAsync(int cityId)
        {
            return await _context.Districts
                .Where(d => d.CityId == cityId)
                .OrderBy(d => d.DistrictName)
                .ToListAsync();
        }
    }
}
