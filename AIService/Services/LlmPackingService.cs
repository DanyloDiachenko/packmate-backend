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
        var model = _config["AiSettings:Model"] ?? "gemini-3.6-flash";
        var maxItems = _config["AiSettings:MaxRecommendations"] ?? "5";

        var existingItemsList = request.ExistingItems != null && request.ExistingItems.Count > 0
            ? string.Join(", ", request.ExistingItems)
            : "None";

        var systemPrompt = $$"""
        You are PackPilot, an expert and practical travel packing assistant.
        Your mission is to recommend high-utility, context-aware, and critical packing items for an upcoming trip based on the traveler's destination, climate, season, current weather forecast, trip type/tags, duration, and local country specifics (e.g., electrical plug/socket types, voltage, local customs, and weather quirks).
        RULES:
        1. SUGGEST AT MOST {{maxItems}} of the most useful, context-driven items. Prioritize items travelers frequently forget (e.g., specific plug adapters, climate layers, rain protection, local essentials).
        2. DO NOT DUPLICATE: Never suggest any items already present in the traveler's checklist: [{{existingItemsList}}]. Perform case-insensitive deduplication against this list.
        3. SECTION CATEGORIES: The "section" field MUST strictly be one of:
        - "clothes"     (Clothes & Footwear)
        - "electro"     (Electronics & Tech)
        - "documents"   (Documents & Money)
        - "toiletries"  (Toiletries & Health)
        - "tools"       (Misc & Gear / Accessories)
        4. TAGS: The "tag" field MUST strictly be one of:
        - "essential"   (Absolute must-have)
        - "important"   (Highly recommended)
        - "safety"      (Health, safety, or legal requirement)
        - "optional"    (Convenience / nice-to-have)
        5. REASONS: The "reason" field must be concise (1 short sentence) explaining specifically *why* based on destination, weather, plug type, or activity.
        6. EMOJI: Provide a single relevant Unicode emoji in "iconEmoji" for each item (e.g., "🔌", "☂️", "🧴", "🥾", "💊").
        7. OUTPUT FORMAT: Output MUST be STRICTLY raw, valid JSON matching the schema below. Do NOT wrap in markdown code blocks (NO ```json), and do NOT include any introductory or concluding text.
        JSON SCHEMA:
        {
            "recommendations": [
                {
                    "section": "electro",
                    "title": "Type C / F Power Adapter",
                    "quantity": 1,
                    "tag": "essential",
                    "iconEmoji": "🔌",
                    "reason": "Spain uses Type C and F electrical outlets at 230V."
                },
                {
                    "section": "tools",
                    "title": "Compact Umbrella",
                    "quantity": 1,
                    "tag": "important",
                    "iconEmoji": "☂️",
                    "reason": "Forecast predicts light rain showers over the weekend."
                }
            ],
            "summaryNotes": "1-2 brief, friendly travel tips tailored to the destination and forecast."
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