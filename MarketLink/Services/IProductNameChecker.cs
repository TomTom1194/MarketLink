namespace MarketLink.Services
{
    // Result of asking AI whether a typed product name (and its unit) fits its category
    public class ProductNameCheckResult
    {
        // true  = the name fits the category and the unit makes sense
        // false = it does not (Reason says why)
        // null  = could not check (no API key, no internet, API error...)
        public bool? Matches { get; set; }

        public string Reason { get; set; } = "";
    }

    public interface IProductNameChecker
    {
        Task<ProductNameCheckResult> CheckAsync(string productName, string categoryName, string unit);
    }
}
