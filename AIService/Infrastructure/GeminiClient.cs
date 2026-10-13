using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIService.Settings;
using Microsoft.Extensions.Options;

namespace AIService.Infrastructure;

public class GeminiClient
{
    private readonly HttpClient _http;
    private readonly AiSettings _settings;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(HttpClient http, IOptions<AiSettings> options, ILogger<GeminiClient> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<string?> GenerateContentAsync(
        string systemPrompt,
        string userMessage,
        double temperature = 0.4,
        CancellationToken ct = default)
    {
        var endpoint = $"models/{_settings.Model}:generateContent?key={_settings.ApiKey}";

        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userMessage } } } },
            generationConfig = new { responseMimeType = "application/json", temperature }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _settings.ApiKey);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini API error {StatusCode}: {Error}", response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var envelope = JsonSerializer.Deserialize<GeminiContentResponse>(json);
        var text = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        return string.IsNullOrWhiteSpace(text) ? null : StripMarkdownFences(text);
    }

    private static string StripMarkdownFences(string raw)
    {
        var s = raw.Trim();
        if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) s = s[7..];
        else if (s.StartsWith("```", StringComparison.OrdinalIgnoreCase)) s = s[3..];
        if (s.EndsWith("```", StringComparison.OrdinalIgnoreCase)) s = s[..^3];
        return s.Trim();
    }
}

public record GeminiContentResponse(
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
