namespace AIService.DTOs;

/// <summary>
/// Request payload for generating AI-powered packing list recommendations.
/// </summary>
/// <param name="City">Destination city name (e.g. Paris, Tokyo).</param>
/// <param name="Country">Destination country name (e.g. France, Japan).</param>
/// <param name="TripDays">Duration of the trip in days.</param>
/// <param name="TripType">Trip category (e.g. leisure, business, camping, skiing).</param>
/// <param name="WeatherSummary">Summary of expected weather or temperature conditions.</param>
/// <param name="CurrentSeason">Seasonal context for the destination.</param>
/// <param name="ExistingItems">Optional list of item titles already packed or planned, to avoid duplication.</param>
public record GenerateRecommendationsRequest(
    string City,
    string Country,
    int TripDays,
    string TripType,
    string? WeatherSummary,
    string? CurrentSeason,
    List<string>? ExistingItems
);

/// <summary>
/// Recommended item suggested by AI.
/// </summary>
/// <param name="Section">Category section. One of: <c>clothes</c>, <c>electro</c>, <c>documents</c>, <c>toiletries</c>, <c>tools</c>.</param>
/// <param name="Title">Name or title of the recommended item.</param>
/// <param name="Quantity">Recommended quantity.</param>
/// <param name="Tag">Priority tag. One of: <c>essential</c>, <c>important</c>, <c>safety</c>, <c>optional</c>.</param>
/// <param name="IconEmoji">Single Unicode emoji representing the item (e.g. 🔌, ☂️, 🧴).</param>
/// <param name="Reason">Concise reason for this recommendation, specific to destination/weather/activity.</param>
public record RecommendationItemDto(
    string Section,
    string Title,
    int Quantity,
    string? Tag,
    string? IconEmoji,
    string? Reason
);

/// <summary>
/// Response payload containing AI-generated packing recommendations and travel advice.
/// </summary>
/// <param name="Recommendations">List of recommended packing items with sections and reasons.</param>
/// <param name="SummaryNotes">Helpful summary tips and destination advice.</param>
public record AIRecommendationResponse(
    List<RecommendationItemDto> Recommendations,
    string SummaryNotes
);

