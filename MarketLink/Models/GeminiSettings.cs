namespace MarketLink.Models
{
    // Settings for the Google Gemini API (appsettings.json -> "Gemini").
    // The ApiKey is kept in user-secrets, not in appsettings.json.
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = "";

        public string Model { get; set; } = "gemini-3.5-flash-lite";
    }
}
