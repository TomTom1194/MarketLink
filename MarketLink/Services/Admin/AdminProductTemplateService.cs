using MarketLink.Data;
using MarketLink.Dtos.Admin;
using MarketLink.Models;
using MarketLink.Services;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    public class AdminProductTemplateService : IAdminProductTemplateService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IProductUnitService _units;

        public AdminProductTemplateService(MarketLinkDbContext context, IProductUnitService units)
        {
            _context = context;
            _units = units;
        }

        public async Task<List<ProductTemplate>> GetTemplatesAsync(int? categoryId)
        {
            return await _context.ProductTemplates
                .Include(t => t.Category)
                .Where(t => categoryId == null || t.CategoryId == categoryId)
                .OrderBy(t => t.Category!.CategoryName)
                .ThenBy(t => t.ProductName)
                .ToListAsync();
        }

        public async Task<List<ProductCategory>> GetCategoriesAsync()
        {
            return await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
        }

        // Units allowed in each category: { categoryId: [units] } (same rules as the farmer's product form)
        public Task<Dictionary<int, List<string>>> GetUnitsByCategoryAsync(IEnumerable<int> categoryIds)
        {
            return _units.GetUnitsByCategoryAsync(categoryIds);
        }

        public async Task<Dictionary<string, string>> ValidateAsync(ProductTemplateFormDto model)
        {
            var errors = new Dictionary<string, string>();
            string name = (model.ProductName ?? "").Trim();   // an empty text box arrives as null

            if (!await _context.ProductCategories.AnyAsync(c => c.CategoryId == model.CategoryId && c.IsActive))
            {
                errors["CategoryId"] = "This category does not exist or is hidden.";
            }
            else if (!await _units.IsAllowedAsync(model.CategoryId, model.Unit))
            {
                errors["Unit"] = "This unit cannot be used for products in this category.";
            }

            if (await _context.ProductTemplates.AnyAsync(t => t.CategoryId == model.CategoryId && t.ProductName == name))
            {
                errors["ProductName"] = "This product is already a template in this category.";
            }

            return errors;
        }

        public async Task CreateAsync(ProductTemplateFormDto model)
        {
            _context.ProductTemplates.Add(new ProductTemplate
            {
                CategoryId = model.CategoryId,
                ProductName = model.ProductName.Trim(),
                Unit = model.Unit,
                IsActive = true
            });
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ToggleAsync(int templateId)
        {
            var template = await _context.ProductTemplates.FindAsync(templateId);
            if (template == null) return false;

            template.IsActive = !template.IsActive;
            await _context.SaveChangesAsync();
            return true;
        }

        // Products copy the name, category and unit, so a template can be deleted safely
        public async Task<bool> DeleteAsync(int templateId)
        {
            var template = await _context.ProductTemplates.FindAsync(templateId);
            if (template == null) return false;

            _context.ProductTemplates.Remove(template);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
