using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MarketLink.Data;
using MarketLink.Dtos.Admin;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    // Admin: product categories. Two levels only: a parent and its children.
    public class AdminCategoryService : IAdminCategoryService
    {
        private readonly MarketLinkDbContext _context;

        public AdminCategoryService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<CategoryRowDto>> GetCategoriesAsync()
        {
            var rows = await _context.ProductCategories
                .Select(c => new CategoryRowDto
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    Slug = c.Slug,
                    ParentId = c.ParentId,
                    ParentName = c.Parent != null ? c.Parent.CategoryName : null,
                    IsActive = c.IsActive,
                    ProductCount = c.Products.Count
                })
                .ToListAsync();

            // Show each parent followed by its children
            var result = new List<CategoryRowDto>();
            foreach (var parent in rows.Where(r => r.ParentId == null).OrderBy(r => r.CategoryName))
            {
                result.Add(parent);
                result.AddRange(rows.Where(r => r.ParentId == parent.CategoryId).OrderBy(r => r.CategoryName));
            }
            return result;
        }

        public async Task<List<ProductCategory>> GetParentOptionsAsync(int? excludeCategoryId)
        {
            return await _context.ProductCategories
                .Where(c => c.ParentId == null && c.CategoryId != (excludeCategoryId ?? 0))
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
        }

        public async Task<CategoryFormDto?> GetFormAsync(int categoryId)
        {
            var category = await _context.ProductCategories.FindAsync(categoryId);
            if (category == null)
            {
                return null;
            }

            return new CategoryFormDto
            {
                CategoryId = category.CategoryId,
                ParentId = category.ParentId,
                CategoryName = category.CategoryName,
                Slug = category.Slug,
                IsActive = category.IsActive
            };
        }

        public async Task<Dictionary<string, string>> ValidateAsync(CategoryFormDto model)
        {
            var errors = new Dictionary<string, string>();
            int currentId = model.CategoryId ?? 0;

            string slug = GetSlug(model);
            if (slug == "")
            {
                errors["Slug"] = "Please enter a slug.";
            }
            else if (await _context.ProductCategories.AnyAsync(c => c.Slug == slug && c.CategoryId != currentId))
            {
                errors["Slug"] = "This slug is already used by another category.";
            }

            if (model.ParentId != null)
            {
                var parent = await _context.ProductCategories.FindAsync(model.ParentId);

                if (parent == null || parent.CategoryId == currentId)
                {
                    errors["ParentId"] = "Please choose a valid parent category.";
                }
                else if (parent.ParentId != null)
                {
                    errors["ParentId"] = "The parent must be a top-level category.";
                }
                else if (await _context.ProductCategories.AnyAsync(c => c.ParentId == currentId))
                {
                    errors["ParentId"] = "This category has sub-categories, so it cannot have a parent.";
                }
            }

            return errors;
        }

        public async Task CreateAsync(CategoryFormDto model)
        {
            var category = new ProductCategory
            {
                ParentId = model.ParentId,
                CategoryName = model.CategoryName.Trim(),
                Slug = GetSlug(model),
                IsActive = model.IsActive
            };

            _context.ProductCategories.Add(category);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(CategoryFormDto model)
        {
            var category = await _context.ProductCategories.FindAsync(model.CategoryId);
            if (category == null)
            {
                return false;
            }

            category.ParentId = model.ParentId;
            category.CategoryName = model.CategoryName.Trim();
            category.Slug = GetSlug(model);
            category.IsActive = model.IsActive;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleActiveAsync(int categoryId)
        {
            var category = await _context.ProductCategories.FindAsync(categoryId);
            if (category == null)
            {
                return false;
            }

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();
            return true;
        }

        // Use the slug typed by the admin, or build one from the name
        private static string GetSlug(CategoryFormDto model)
        {
            return string.IsNullOrWhiteSpace(model.Slug)
                ? MakeSlug(model.CategoryName)
                : model.Slug.Trim().ToLower();
        }

        // "Rau củ & Đồ khô" -> "rau-cu-do-kho"
        private static string MakeSlug(string text)
        {
            string lower = text.Trim().ToLower().Replace("đ", "d");

            // Remove Vietnamese accents: "củ" -> "cu"
            var builder = new StringBuilder();
            foreach (char c in lower.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            string slug = Regex.Replace(builder.ToString(), "[^a-z0-9]+", "-");
            return slug.Trim('-');
        }
    }
}
