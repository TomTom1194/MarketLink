using System.Text.Json;

namespace MarketLink.Services
{
    // The question sent to the AI and the reading of its answer (used by GeminiProductNameChecker)
    public static class ProductNameCheckPrompt
    {
        public static string Build(string productName, string categoryName, string unit)
        {
            return
                "You check product listings on a website where Vietnamese farmers sell fresh produce at local markets.\n" +
                "Category: " + categoryName + "\n" +
                "Product name typed by the farmer: " + productName + "\n" +
                "Sold per: " + unit + "\n\n" +
                "Answer match = true only if BOTH are true:\n" +
                "1) it is a real food / farm product that belongs to this category (the name may be in Vietnamese or English);\n" +
                "2) the unit is a normal way to sell this product at a market (e.g. eggs per dozen or tray, not per liter; " +
                "lettuce per head, bunch or kg, not per dozen).\n" +
                "Answer with JSON only, no other text:\n" +
                "{\"match\": true or false, \"reason\": \"one short sentence in English\"}";
        }

        // Reads {"match": ..., "reason": ...} out of the AI text. Returns null when it cannot be read.
        public static ProductNameCheckResult? Parse(string text)
        {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;

            try
            {
                using var answer = JsonDocument.Parse(text.Substring(start, end - start + 1));
                return new ProductNameCheckResult
                {
                    Matches = answer.RootElement.GetProperty("match").GetBoolean(),
                    Reason = answer.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : ""
                };
            }
            catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                return null;
            }
        }
    }
}
