using MarketLink.Dtos.Admin;
using MarketLink.Models;

namespace MarketLink.Services.Admin
{
    public interface IAdminMarketService
    {
        Task<List<MarketRowDto>> GetMarketsAsync(string? search, int? cityId);

        // null when the market does not exist
        Task<MarketFormDto?> GetFormAsync(int marketId);

        // Errors keyed by form field name; empty = valid
        Task<Dictionary<string, string>> ValidateAsync(MarketFormDto model);

        Task CreateAsync(MarketFormDto model);

        Task<bool> UpdateAsync(MarketFormDto model);

        // Show / hide the market for customers
        Task<bool> ToggleActiveAsync(int marketId);

        Task<List<City>> GetCitiesAsync();

        Task<List<District>> GetDistrictsAsync(int cityId);
    }
}
