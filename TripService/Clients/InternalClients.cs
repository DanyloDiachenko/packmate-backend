namespace TripService.Clients;

/// <summary>
/// Weather information retrieved from the Weather Service.
/// </summary>
/// <param name="WeatherToday">Current weather summary.</param>
/// <param name="CurrentSeason">Current season name.</param>
/// <param name="ConditionDescription">Detailed condition description.</param>
public record WeatherDto(string WeatherToday, string CurrentSeason, string ConditionDescription);

/// <summary>
/// Summary of a trip item.
/// </summary>
/// <param name="Label">Item label or title.</param>
public record TripItemSummaryDto(string Label);

/// <summary>
/// Group of trip items by category/section.
/// </summary>
/// <param name="Section">Section name (e.g. clothes, electronics).</param>
/// <param name="Items">List of item summaries.</param>
public record TripSectionGroupDto(string Section, List<TripItemSummaryDto> Items);

/// <summary>
/// Request payload sent to the AI recommendation service.
/// </summary>
/// <param name="City">Destination city.</param>
/// <param name="Country">Destination country.</param>
/// <param name="TripDays">Calculated duration of trip in days.</param>
/// <param name="TripType">Type of trip.</param>
/// <param name="WeatherSummary">Weather overview.</param>
/// <param name="CurrentSeason">Season.</param>
/// <param name="ExistingItems">List of item labels already on packing list.</param>
/// <param name="Tags">Optional list of trip tags.</param>
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

/// <summary>
/// An AI recommended packing item.
/// </summary>
/// <param name="Section">Category section (e.g. Clothing, Toiletries, Tech).</param>
/// <param name="Label">Name of the item.</param>
/// <param name="Quantity">Recommended quantity.</param>
/// <param name="Tag">Optional tag (e.g. Essential, Weather, Activity).</param>
/// <param name="Reason">Explanation why this item is recommended.</param>
public record RecommendedItemDto(string Section, string Label, int Quantity, string? Tag, string? Reason);

/// <summary>
/// AI generated packing recommendations response.
/// </summary>
/// <param name="Recommendations">List of recommended packing items.</param>
/// <param name="SummaryNotes">Helpful tips and advice for the trip.</param>
public record AiRecommendationResponseDto(List<RecommendedItemDto> Recommendations, string SummaryNotes);