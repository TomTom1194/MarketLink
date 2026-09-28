using MarketLink.Dtos.Admin;
using MarketLink.Models;

namespace MarketLink.Services.Admin
{
    public interface IAdminProductTemplateService
    {
        // All templates, optionally only one category
        Task<List<ProductTemplate>> GetTemplatesAsync(int? categoryId);

        Task<List<ProductCategory>> GetCategoriesAsync();

        // Units allowed in each category: { categoryId: [units] }
        Task<Dictionary<int, List<string>>> GetUnitsByCategoryAsync(IEnumerable<int> categoryIds);

        // Returns errors by field name (empty = OK)
        Task<Dictionary<string, string>> ValidateAsync(ProductTemplateFormDto model);

        Task CreateAsync(ProductTemplateFormDto model);

        // Show / hide a template in the farmer's list
        Task<bool> ToggleAsync(int templateId);

        Task<bool> DeleteAsync(int templateId);
    }
}
