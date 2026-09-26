using MarketLink.Dtos.Admin;
using MarketLink.Models;

namespace MarketLink.Services.Admin
{
    public interface IAdminCategoryService
    {
        Task<List<CategoryRowDto>> GetCategoriesAsync();

        // Top-level categories that can be chosen as a parent (never the category itself)
        Task<List<ProductCategory>> GetParentOptionsAsync(int? excludeCategoryId);

        // null when the category does not exist
        Task<CategoryFormDto?> GetFormAsync(int categoryId);

        // Errors keyed by form field name; empty = valid
        Task<Dictionary<string, string>> ValidateAsync(CategoryFormDto model);

        Task CreateAsync(CategoryFormDto model);

        Task<bool> UpdateAsync(CategoryFormDto model);

        // Show / hide the category
        Task<bool> ToggleActiveAsync(int categoryId);
    }
}
