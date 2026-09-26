using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Dtos.Admin;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    // Farmer sign-up: farmers send an application (farm + market + stall)
    // and wait for an admin to approve it. The password is emailed after approval.
    public class FarmerAccountService : IFarmerAccountService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IAuthService _authService;

        public FarmerAccountService(MarketLinkDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<Dictionary<string, string>> ValidateApplicationAsync(FarmerApplicationDto model)
        {
            var errors = new Dictionary<string, string>();

            // ----- Farm -----
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

            // ----- Market -----
            // Days the market is open; used below to check the stall's selling days
            List<int> marketDays = new List<int>();

            // (When MarketId is empty, [Required] on the DTO already shows the error.)
            if (model.MarketId != null && model.IsNewMarket)
            {
                marketDays = model.NewMarketOpenDays;
                ValidateNewMarket(model, errors);

                if (!errors.ContainsKey("NewMarketName") && model.NewMarketDistrictId != null)
                {
                    string name = model.NewMarketName!.Trim();
                    var sameName = await _context.Markets
                        .FirstOrDefaultAsync(m => m.DistrictId == model.NewMarketDistrictId && m.MarketName == name);

                    if (sameName != null && sameName.IsActive)
                        errors["NewMarketName"] = "This market is already listed. Please choose it from the market list.";
                    else if (sameName != null)
                        errors["NewMarketName"] = "This market was already suggested and is waiting for review. Please contact us.";
                }

                if (model.NewMarketDistrictId != null &&
                    !await _context.Districts.AnyAsync(d => d.DistrictId == model.NewMarketDistrictId && d.CityId == model.NewMarketCityId))
                {
                    errors["NewMarketDistrictId"] = "The district does not belong to the selected city.";
                }
            }
            else if (model.MarketId != null)
            {
                var market = await _context.Markets
                    .FirstOrDefaultAsync(m => m.MarketId == model.MarketId && m.IsActive);

                if (market == null)
                {
                    errors["MarketId"] = "Please choose a market from the list.";
                }
                else
                {
                    marketDays = MarketDays.FromText(market.OpenDays);

                    // Stall codes are unique inside one market
                    string code = model.StallCode.Trim();
                    if (await _context.Stalls.AnyAsync(s => s.MarketId == market.MarketId && s.StallCode == code))
                    {
                        errors["StallCode"] = "This stall code is already registered at this market.";
                    }
                }
            }

            // ----- Stall -----
            if (model.SellingDays.Count == 0)
            {
                errors["SellingDays"] = "Please tick at least one day you sell.";
            }
            else if (marketDays.Count > 0)
            {
                // You can only sell on days the market is open
                var closedDays = model.SellingDays.Where(d => !marketDays.Contains(d)).ToList();
                if (closedDays.Count > 0)
                {
                    errors["SellingDays"] = "The market is not open on: " + MarketDays.ToDisplay(MarketDays.ToText(closedDays)) + ".";
                }
            }

            return errors;
        }

        public async Task SubmitApplicationAsync(FarmerApplicationDto model)
        {
            // The Users table needs a password, so we store a long random one that nobody knows.
            // It is replaced by a new password when the admin approves the application.
            string placeholderPassword = PasswordGenerator.Generate(24);

            var user = await _authService.CreateUserAsync("farmer", model.Email, model.Phone, placeholderPassword);

            var farmer = new FarmerProfile
            {
                BrandName = model.BrandName.Trim(),
                ContactPerson = model.ContactPerson.Trim(),
                Address = model.Address.Trim(),
                DistrictId = model.DistrictId!.Value,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                ApprovalStatus = "pending"
            };
            user.FarmerProfile = farmer;

            // Existing market, or a new one that stays hidden until the admin approves this farmer
            Market market;
            if (model.IsNewMarket)
            {
                market = new Market
                {
                    DistrictId = model.NewMarketDistrictId!.Value,
                    MarketName = model.NewMarketName!.Trim(),
                    MapUrl = model.NewMarketMapUrl!.Trim(),
                    OpenDays = MarketDays.ToText(model.NewMarketOpenDays),
                    OpenTime = model.NewMarketOpenTime!.Value,
                    CloseTime = model.NewMarketCloseTime!.Value,
                    IsActive = false,
                    RequestedByUser = user
                };
            }
            else
            {
                market = (await _context.Markets.FindAsync(model.MarketId))!;
            }

            var stall = new Stall
            {
                Market = market,
                Farmer = farmer,
                StallCode = model.StallCode.Trim(),
                GoogleUrl = string.IsNullOrWhiteSpace(model.StallGoogleUrl) ? null : model.StallGoogleUrl.Trim(),
                LocationNote = string.IsNullOrWhiteSpace(model.StallLocationNote) ? null : model.StallLocationNote.Trim(),
                SellingDays = MarketDays.ToText(model.SellingDays),
                IsActive = true
            };

            // One SaveChanges = one transaction: user, farmer profile, market and stall are saved together
            _context.Users.Add(user);
            _context.Stalls.Add(stall);
            await _context.SaveChangesAsync();
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

        public async Task<List<Market>> GetActiveMarketsAsync()
        {
            return await _context.Markets
                .Include(m => m.District)
                    .ThenInclude(d => d!.City)
                .Where(m => m.IsActive)
                .OrderBy(m => m.District!.City!.CityName)
                .ThenBy(m => m.District!.DistrictName)
                .ThenBy(m => m.MarketName)
                .ToListAsync();
        }


        // Fields that are required only when "Other" is chosen
        private static void ValidateNewMarket(FarmerApplicationDto model, Dictionary<string, string> errors)
        {
            if (string.IsNullOrWhiteSpace(model.NewMarketName))
                errors["NewMarketName"] = "Please enter the market name.";

            if (model.NewMarketCityId == null)
                errors["NewMarketCityId"] = "Please choose the city of the market.";

            if (model.NewMarketDistrictId == null)
                errors["NewMarketDistrictId"] = "Please choose the district of the market.";

            if (string.IsNullOrWhiteSpace(model.NewMarketMapUrl))
                errors["NewMarketMapUrl"] = "Please paste the Google Maps link of the market.";

            if (model.NewMarketOpenDays.Count == 0)
                errors["NewMarketOpenDays"] = "Please tick at least one market day.";

            if (model.NewMarketOpenTime == null)
                errors["NewMarketOpenTime"] = "Please enter the opening time.";

            if (model.NewMarketCloseTime == null)
                errors["NewMarketCloseTime"] = "Please enter the closing time.";
            else if (model.NewMarketOpenTime != null && model.NewMarketCloseTime <= model.NewMarketOpenTime)
                errors["NewMarketCloseTime"] = "Closing time must be later than opening time.";
        }
    }
}
