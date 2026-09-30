using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIService.DTOs;

namespace AIService.Services;

public interface IAIService
{
    Task<AIRecommendationResponse> GenerateRecommendationsAsync(GenerateRecommendationsRequest request, CancellationToken ct = default);
}

public class LlmPackingService : IAIService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<LlmPackingService> _logger;

    public LlmPackingService(HttpClient http, IConfiguration config, ILogger<LlmPackingService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<AIRecommendationResponse> GenerateRecommendationsAsync(GenerateRecommendationsRequest request, CancellationToken ct = default)
    {
        var apiKey = _config["AiSettings:ApiKey"] ?? string.Empty;
        var model = _config["AiSettings:Model"] ?? "gemini-2.0-flash";
        var maxItems = _config["AiSettings:MaxRecommendations"] ?? "10";

        var existingItemsList = request.ExistingItems != null && request.ExistingItems.Count > 0
            ? string.Join(", ", request.ExistingItems)
            : "None";

        var systemPrompt = $$"""
        You are PackPilot, a smart and practical travel packing assistant.
        Your task is to recommend useful, critical, and context-aware items to pack for an upcoming trip, taking into account the destination, climate, weather forecast, trip type, duration, and country-specific considerations (plug/socket types, local laws, weather peculiarities).

        RULES:
        1. Suggest at most {{maxItems}} of the MOST critical and practical items.
        2. Do NOT suggest items that are already packed: [{{existingItemsList}}].
        3. The category ("section") must be strictly one of: ["clothes", "electro", "documents", "toiletries", "tools"].
        4. Output MUST be strictly valid JSON according to this schema:
        {
            "recommendations": [
                {
                    "section": "tools",
                    "title": "Compact umbrella",
                    "quantity": 1,
                    "tag": "important",
                    "reason": "Forecast indicates frequent rain"
                }
            ],
            "summaryNotes": "Brief travel advice for the traveler in 1-2 sentences."
        }
        """;

        var userMessage = $"""
        Trip Destination: {request.City}, {request.Country}
        Duration: {request.TripDays} days
        Trip Type: {request.TripType}
        Season: {request.CurrentSeason ?? "Not specified"}
        Weather / Forecast: {request.WeatherSummary ?? "Typical for this season"}
        """;

        var payload = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = userMessage }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.5
            }
        };

        var baseUrl = _config["AiSettings:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains("gemini.google.com"))
        {
            baseUrl = "https://generativelanguage.googleapis.com/v1beta";
        }

        var endpoint = $"{baseUrl}/models/{model}:generateContent?key={apiKey}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Add("x-goog-api-key", apiKey);
        }

        var response = await _http.SendAsync(requestMessage, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini API request failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseJson);

        var contentString = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(contentString))
        {
            _logger.LogWarning("Gemini API returned an empty response.");
            return new AIRecommendationResponse(new List<RecommendationItemDto>(), "Failed to generate recommendations from AI.");
        }

        var cleanedJson = contentString.Trim();
        if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(7);
        }
        else if (cleanedJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(3);
        }

        if (cleanedJson.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
        }

        cleanedJson = cleanedJson.Trim();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var recommendations = JsonSerializer.Deserialize<AIRecommendationResponse>(cleanedJson, options);

        return recommendations ?? new AIRecommendationResponse(new List<RecommendationItemDto>(), string.Empty);
    }
}

public record GeminiGenerateContentResponse(
    [property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates
);

public record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content,
    [property: JsonPropertyName("finishReason")] string? FinishReason
);

public record GeminiContent(
    [property: JsonPropertyName("parts")] List<GeminiPart>? Parts,
    [property: JsonPropertyName("role")] string? Role
);

public record GeminiPart(
    [property: JsonPropertyName("text")] string? Text
);