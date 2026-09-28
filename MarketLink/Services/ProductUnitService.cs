using MarketLink.Data;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class ProductUnitService : IProductUnitService
    {
        private readonly MarketLinkDbContext _context;

        public ProductUnitService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public Task<List<string>> GetUnitsAsync()
        {
            return _context.Database.SqlQuery<string>(
                $"SELECT [unit] AS [Value] FROM [dbo].[Product_Unit] WHERE [is_active] = 1 ORDER BY [sort_order]")
                .ToListAsync();
        }

        public async Task<List<string>> GetUnitsForCategoryAsync(int categoryId)
        {
            var units = await _context.Database.SqlQuery<string>(
                $@"SELECT u.[unit] AS [Value]
                   FROM [dbo].[Category_Unit] cu
                   JOIN [dbo].[Product_Unit] u ON u.[unit] = cu.[unit]
                   WHERE cu.[category_id] = {categoryId} AND u.[is_active] = 1
                   ORDER BY u.[sort_order]")
                .ToListAsync();

            // No units set for this category yet -> every unit is allowed
            return units.Count > 0 ? units : await GetUnitsAsync();
        }

        public async Task<Dictionary<int, List<string>>> GetUnitsByCategoryAsync(IEnumerable<int> categoryIds)
        {
            var result = new Dictionary<int, List<string>>();
            foreach (var categoryId in categoryIds.Distinct())
            {
                result[categoryId] = await GetUnitsForCategoryAsync(categoryId);
            }
            return result;
        }

        public async Task<bool> IsAllowedAsync(int categoryId, string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return false;
            return (await GetUnitsForCategoryAsync(categoryId)).Contains(unit.Trim());
        }
    }
}
