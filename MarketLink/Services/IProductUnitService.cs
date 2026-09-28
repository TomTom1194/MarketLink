namespace MarketLink.Services
{
    // Units (kg, bunch, dozen...) and which of them each category may use.
    // Tables: Product_Unit (all units) and Category_Unit (units allowed per category).
    public interface IProductUnitService
    {
        // Every active unit
        Task<List<string>> GetUnitsAsync();

        // Units allowed in one category.
        // A category with no rows in Category_Unit can use every active unit.
        Task<List<string>> GetUnitsForCategoryAsync(int categoryId);

        // Units for several categories at once: { categoryId: [units] } (used by the unit dropdown script)
        Task<Dictionary<int, List<string>>> GetUnitsByCategoryAsync(IEnumerable<int> categoryIds);

        Task<bool> IsAllowedAsync(int categoryId, string? unit);
    }
}
