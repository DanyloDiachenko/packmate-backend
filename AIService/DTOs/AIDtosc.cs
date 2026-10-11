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

public record ExistingTripDateDto(
    Guid? Id,
    string Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate
);

public record TripGeneratorRequest(
    string Message,
    TripSkeletonDto? Skeleton = null
);

public record TripGeneratorResponse(
    bool Success,
    string? SuccessMessage,
    string? ErrorMessage,
    List<string>? MissingFields,
    TripSkeletonDto? Skeleton,
    List<string>? NextPossibleActions
);

public record TripSkeletonDto(
    string City,
    string Country,
    string? CountryFlag,
    string? BannerImage,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    int DurationDays,
    List<string>? Tags,
    string? WeatherSummary,
    string? CurrentSeason,
    List<RecommendationItemDto>? Items,
    Guid? TripId = null
);

public record TripServiceDestinationDto(
    string City,
    string Country
);

public record TripServiceTripDto(
    Guid Id,
    TripServiceDestinationDto Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate
);

public record TripServicePaginatedResponse(
    int TotalItems,
    List<TripServiceTripDto> Trips
);
