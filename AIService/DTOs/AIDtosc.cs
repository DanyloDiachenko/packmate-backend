namespace AIService.DTOs;











public record GenerateRecommendationsRequest(
    string City,
    string Country,
    int TripDays,
    string TripType,
    string? WeatherSummary,
    string? CurrentSeason,
    List<string>? ExistingItems
);










public record RecommendationItemDto(
    string Section,
    string Title,
    int Quantity,
    string? Tag,
    string? IconEmoji,
    string? Reason
);






public record AIRecommendationResponse(
    List<RecommendationItemDto> Recommendations,
    string SummaryNotes
);

