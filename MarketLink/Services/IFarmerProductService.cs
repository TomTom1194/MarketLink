using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IFarmerProductService
    {
        Task<List<ProductCategory>> GetCategoriesAsync(int farmerId, int? keepCategoryId = null);

        // Ready-made products in the categories this farmer registered for
        Task<List<ProductTemplate>> GetTemplatesAsync(int farmerId);
        Task<ProductTemplate?> GetTemplateAsync(int farmerId, int templateId);

        Task<List<ProductExp>> GetExpiryOptionsAsync();

        Task<List<Product>> GetProductsAsync(int farmerId);

        Task<Product?> GetProductAsync(int farmerId, int productId);

        Task<Product> CreateProductAsync(int farmerId,CreateFarmerProductDto model);

        Task<bool> UpdateProductAsync(int farmerId,UpdateFarmerProductDto model);

        Task<bool> UpdateProductStatusAsync( int farmerId,UpdateFarmerProductStatusDto model);

        Task<StockPrice> CreateInitialStockPriceAsync(int farmerId,CreateInitialStockPriceDto model);

        Task<bool> ChangePriceAsync(int farmerId,ChangeFarmerProductPriceDto model);

        Task<bool> ReupAsync(int farmerId, ReupFarmerProductDto model);

        Task<List<StockPrice>> GetStockHistoryAsync( int farmerId,int productId);

        // Called after an order changes the reserved / sold quantities.
        Task HideIfUnavailableAsync(int productId);
    }
}
