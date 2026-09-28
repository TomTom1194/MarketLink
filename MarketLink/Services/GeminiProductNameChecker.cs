using System.Text;
using System.Text.Json;
using MarketLink.Models;
using Microsoft.Extensions.Options;

namespace MarketLink.Services
{
    
    public class GeminiProductNameChecker : IProductNameChecker
    {
        private readonly HttpClient _http;
        private readonly GeminiSettings _settings;
        private readonly ILogger<GeminiProductNameChecker> _logger;

        public GeminiProductNameChecker(HttpClient http, IOptions<GeminiSettings> settings, ILogger<GeminiProductNameChecker> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<ProductNameCheckResult> CheckAsync(string productName, string categoryName, string unit)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                return new ProductNameCheckResult { Matches = null, Reason = "No Gemini API key is set." };
            }

            var body = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = ProductNameCheckPrompt.Build(productName, categoryName, unit) } } }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",   
                    maxOutputTokens = 1024
                }
            };

            
            var models = new List<string> { _settings.Model };
            if (!string.IsNullOrWhiteSpace(_settings.FallbackModel) && _settings.FallbackModel != _settings.Model)
            {
                models.Add(_settings.FallbackModel);
            }

            var result = new ProductNameCheckResult { Matches = null, Reason = "The AI check is not available right now." };
            foreach (string model in models)
            {
                result = await AskModelAsync(model, body);
                if (result.Matches != null)
                {
                    return result;   
                }
            }
            return result;   
        }

        // Sends the question to one Gemini model.
        // Matches = null when this model could not answer (error, timeout, unreadable answer).
        private async Task<ProductNameCheckResult> AskModelAsync(string model, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1beta/models/" + model + ":generateContent");
            request.Headers.Add("x-goog-api-key", _settings.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            try
            {
                using var response = await _http.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini model {Model} returned {Status}: {Body}", model, (int)response.StatusCode, json);
                    return new ProductNameCheckResult { Matches = null, Reason = $"The AI check is not available right now (Gemini error {(int)response.StatusCode})." };
                }

                // Response: { "candidates": [ { "content": { "parts": [ { "text": "{\"match\": true, ...}" } ] } } ] }
                using var doc = JsonDocument.Parse(json);
                string text = doc.RootElement.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString() ?? "";

                return ProductNameCheckPrompt.Parse(text)
                    ?? new ProductNameCheckResult { Matches = null, Reason = "The AI answer could not be read." };
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is IndexOutOfRangeException)
            {
                _logger.LogWarning(ex, "Could not check product name with Gemini model {Model}", model);
                return new ProductNameCheckResult { Matches = null, Reason = $"The AI check is not available right now ({ex.GetType().Name})." };
            }
        }
    }
}
