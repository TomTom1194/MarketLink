using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IFarmerAccountService
    {
        // Errors keyed by form field name; empty = valid
        Task<Dictionary<string, string>> ValidateApplicationAsync(FarmerApplicationDto model);

        // Creates the farmer account (status "pending"), the stall,
        // and the new market when "Other" was chosen (hidden until approval)
        Task SubmitApplicationAsync(FarmerApplicationDto model);

        Task<List<City>> GetCitiesAsync();

        Task<List<District>> GetDistrictsAsync(int cityId);

        // All active markets, with district and city (for the market dropdown)
        Task<List<Market>> GetActiveMarketsAsync();

    }
}
