using MarketLink.Dtos.Admin;
using MarketLink.Models;

namespace MarketLink.Services.Admin
{
    public interface IAdminProductExpService
    {
        Task<List<ProductExp>> GetAllAsync();

        // Number of products using each period, keyed by ExpId
        Task<Dictionary<int, int>> CountProductsAsync();

        // null when the period does not exist
        Task<ProductExpFormDto?> GetFormAsync(int expId);

        // Errors keyed by form field name; empty = valid
        Task<Dictionary<string, string>> ValidateAsync(ProductExpFormDto model);

        Task CreateAsync(ProductExpFormDto model);

        Task<bool> UpdateAsync(ProductExpFormDto model);

        // false when the period is missing or still used by products
        Task<bool> DeleteAsync(int expId);
    }
}
