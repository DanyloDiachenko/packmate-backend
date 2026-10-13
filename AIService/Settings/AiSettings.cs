namespace AIService.Settings;

public class AiSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gemini-2.0-flash";
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";
    public int MaxRecommendations { get; init; } = 5;
}
