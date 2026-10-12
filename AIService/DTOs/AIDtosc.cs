using System.Text.Json.Serialization;

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

public record CopilotExistingItemDto(
    Guid Id,
    string Title,
    string Section,
    int Quantity,
    bool IsPacked,
    string? Tag = null
);

public record AIPackingCopilotRequest(
    Guid TripId,
    string Prompt,
    string? Destination = null,
    string? DateRange = null,
    int? NightsCount = null,
    List<CopilotExistingItemDto>? ExistingItems = null
);

public record CopilotAdditionDto(
    Guid Id,
    string Section,
    string Title,
    int Quantity,
    string? Tag,
    string Reason
);

public record CopilotRemovalDto(
    Guid Id,
    Guid ItemId,
    string Title,
    string Section,
    string Reason
);

public record CopilotModificationDto(
    Guid Id,
    Guid ItemId,
    string Title,
    string Section,
    int CurrentQuantity,
    int NewQuantity,
    string? NewTag,
    string Reason
);

public record AIPackingCopilotResponse(
    bool Success,
    string? Explanation,
    List<CopilotAdditionDto>? Additions,
    List<CopilotRemovalDto>? Removals,
    List<CopilotModificationDto>? Modifications,
    List<string>? NextActions,
    string? ErrorMessage = null
);

public record GeminiCopilotDiffOutput(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("additions")] List<GeminiAdditionDto>? Additions,
    [property: JsonPropertyName("removals")] List<GeminiRemovalDto>? Removals,
    [property: JsonPropertyName("modifications")] List<GeminiModificationDto>? Modifications,
    [property: JsonPropertyName("nextActions")] List<string>? NextActions,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage
);

public record GeminiAdditionDto(
    [property: JsonPropertyName("section")] string Section,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("tag")] string? Tag,
    [property: JsonPropertyName("reason")] string Reason
);

public record GeminiRemovalDto(
    [property: JsonPropertyName("itemId")] string? ItemId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("section")] string? Section,
    [property: JsonPropertyName("reason")] string Reason
);

public record GeminiModificationDto(
    [property: JsonPropertyName("itemId")] string? ItemId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("section")] string? Section,
    [property: JsonPropertyName("newQuantity")] int NewQuantity,
    [property: JsonPropertyName("newTag")] string? NewTag,
    [property: JsonPropertyName("reason")] string Reason
);

public record TripItemResponseDto(
    Guid Id,
    Guid TripId,
    string Section,
    string Title,
    int Quantity,
    bool IsTaken,
    string? Tag
);

public record TripSectionGroupDto(
    string Section,
    List<TripItemResponseDto> Items
);

public record CreateTripItemRequestDto(
    string Section,
    string Title,
    int Quantity = 1,
    string? Tag = null
);

public record SyncUpdateItemRequestDto(
    Guid Id,
    string? Section = null,
    string? Title = null,
    int? Quantity = null,
    string? Tag = null
);

public record SyncTripItemsRequestDto(
    List<CreateTripItemRequestDto>? ItemsToAdd = null,
    List<Guid>? ItemIdsToRemove = null,
    List<SyncUpdateItemRequestDto>? ItemsToUpdate = null
);

public record TripDetailDestinationDto(
    string City,
    string Country,
    string? CountryFlag,
    string? BannerImage
);

public record TripDetailResponseDto(
    Guid Id,
    Guid UserId,
    TripDetailDestinationDto Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    List<string> Tags,
    bool IsArchived,
    DateTime CreatedAt
);

