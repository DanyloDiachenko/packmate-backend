namespace TripService.Clients;







public record WeatherDto(string WeatherToday, string CurrentSeason, string ConditionDescription);





public record TripItemSummaryDto(string Label);






public record TripSectionGroupDto(string Section, List<TripItemSummaryDto> Items);












public record AiRecommendationRequestDto(
    string City,
    string Country,
    int TripDays,
    string TripType,
    string WeatherSummary,
    string CurrentSeason,
    List<string>? ExistingItems,
    List<string>? Tags = null
);









public record RecommendedItemDto(string Section, string Label, int Quantity, string? Tag, string? Reason);






public record AiRecommendationResponseDto(List<RecommendedItemDto> Recommendations, string SummaryNotes);