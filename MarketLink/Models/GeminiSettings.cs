namespace MarketLink.Models
{
    
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = "";

        public string Model { get; set; } = "gemini-3.1-flash-lite";

        
        public string FallbackModel { get; set; } = "gemini-3.5-flash";
    }
}
