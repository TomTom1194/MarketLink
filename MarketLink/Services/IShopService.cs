using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IShopService
    {
        Task<List<City>> GetCitiesAsync();

        Task<List<District>> GetDistrictsAsync(int cityId);

        Task<List<Market>> GetMarketsAsync(int districtId);

        Task<Market?> GetMarketAsync(int marketId);

        Task<List<ProductCategory>> GetCategoriesAsync();

        Task<List<StockPrice>> SearchProductsAsync(int marketId, string? keyword, int? categoryId, string? sort);

        Task<StockPrice?> GetProductDetailAsync(int stockPriceId);

        Task<List<StockPrice>> GetOtherProductsOfStallAsync(int stallId, int stockPriceId);

        Task<List<Market>> GetActiveMarketsAsync(int take);

        Task<List<StockPrice>> GetNewestProductsAsync(int? marketId, int take);

        Task<List<FarmerProfile>> GetFeaturedFarmersAsync(int take);

        Task<List<MarketDistanceDto>> GetNearestMarketsAsync(double latitude, double longitude);
    }
}
