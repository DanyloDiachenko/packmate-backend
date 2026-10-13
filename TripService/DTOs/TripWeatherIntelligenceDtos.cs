using System;
using System.Collections.Generic;

namespace TripService.DTOs;

public record WeatherHighlightDto(
    string Icon,
    string IconColor,
    string Title,
    string Detail
);

public record WeatherProposalDto(
    Guid Id,
    string Title,
    string Section,
    int Quantity,
    string Tag,
    string Reason
);

public record WeatherIntelligenceResponseDto(
    bool HasActiveImpact,
    Guid? ImpactId,
    DateTime? DetectedAt,
    List<WeatherHighlightDto> Highlights,
    List<WeatherProposalDto> Proposals,
    string? ExistingMatchedNotice
);

public record ApplyWeatherAdjustmentsRequestDto(
    Guid ImpactId,
    List<Guid> SelectedItemIds
);

public record AddedTripItemDto(
    Guid Id,
    string Section,
    string Title,
    int Quantity,
    string Tag,
    bool IsPacked
);

public record ApplyWeatherAdjustmentsResponseDto(
    bool Success,
    int AddedCount,
    string Message,
    List<AddedTripItemDto> AddedItems
);

public record ExternalCreateNotificationRequestDto(
    Guid UserId,
    Guid? TripId,
    string? Category,
    string Title,
    string Body,
    string? ActionTitle,
    string? RelatedDestination,
    Guid? ImpactId,
    bool SendPush = true
);

public record SimpleSuccessResponseDto(
    bool Success
);

public record WeatherProcessResult(
    bool Success,
    string? ErrorMessage = null,
    bool HasWeatherImpact = false
)
{
    public static WeatherProcessResult Ok(bool hasWeatherImpact = false) => new(true, null, hasWeatherImpact);
    public static WeatherProcessResult Failure(string errorMessage) => new(false, errorMessage, false);

    public static implicit operator bool(WeatherProcessResult result) => result.Success;
};

public record ExternalTripItemDto(
    Guid Id,
    Guid TripId,
    string Section,
    string Title,
    int Quantity,
    bool IsTaken,
    string? Tag
);

public record ExternalTripSectionGroupDto(
    string Section,
    List<ExternalTripItemDto> Items
);

public record ExternalCreateTripItemDto(
    string Section,
    string Title,
    int Quantity,
    string? Tag
);

public record ExternalBulkCreateTripItemsPayload(
    List<ExternalCreateTripItemDto> Items
);

public record WeatherRepackForecastDeltaItemDto(
    string Date,
    int? RainProbability,
    int? NightTemp,
    int? DayTemp,
    int? UvIndex,
    int? WindSpeed,
    string? Condition,
    string Delta
);

public record WeatherRepackRequestPayload(
    string Destination,
    string Dates,
    List<WeatherRepackForecastDeltaItemDto> ForecastDelta,
    List<string> ExistingTripItems
);

public record WeatherRepackResponsePayload(
    List<WeatherHighlightDto> Highlights,
    List<WeatherProposalDto> Proposals,
    string? ExistingMatchedNotice
);

public record ExternalDailyForecastItemDto(
    DateOnly Date,
    string? DayOfWeek,
    int HighTemp,
    int LowTemp,
    string Condition,
    string IconName,
    int RainProbability,
    int UvIndex,
    int WindSpeed
);

public record ExternalTripDailyForecastResponse(
    string City,
    string Country,
    List<ExternalDailyForecastItemDto> Forecasts
);
