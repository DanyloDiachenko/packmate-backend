using Microsoft.EntityFrameworkCore;
using TripService.Data;
using TripService.DTOs;
using TripService.Entities;
using TripService.Infrastructure;

namespace TripService.Services;

public interface IWeatherIntelligenceService
{
    Task<WeatherProcessResult> CheckAndProcessTripAsync(Guid tripId, bool force = false, CancellationToken ct = default);
    Task<WeatherIntelligenceResponseDto> GetWeatherIntelligenceAsync(Guid tripId, CancellationToken ct = default);
    Task<ApplyWeatherAdjustmentsResponseDto> ApplyWeatherAdjustmentsAsync(Guid tripId, ApplyWeatherAdjustmentsRequestDto request, CancellationToken ct = default);
}

public class WeatherIntelligenceService : IWeatherIntelligenceService
{
    private readonly TripDbContext _db;
    private readonly IWeatherServiceClient _weatherClient;
    private readonly IAiServiceClient _aiClient;
    private readonly ITripItemsServiceClient _tripItemsClient;
    private readonly INotificationServiceClient _notificationClient;
    private readonly ILogger<WeatherIntelligenceService> _logger;

    public WeatherIntelligenceService(
        TripDbContext db,
        IWeatherServiceClient weatherClient,
        IAiServiceClient aiClient,
        ITripItemsServiceClient tripItemsClient,
        INotificationServiceClient notificationClient,
        ILogger<WeatherIntelligenceService> logger)
    {
        _db = db;
        _weatherClient = weatherClient;
        _aiClient = aiClient;
        _tripItemsClient = tripItemsClient;
        _notificationClient = notificationClient;
        _logger = logger;
    }

    public async Task<WeatherProcessResult> CheckAndProcessTripAsync(Guid tripId, bool force = false, CancellationToken ct = default)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == tripId, ct);
        if (trip is null) return WeatherProcessResult.Failure("Trip not found.");
        if (trip.IsArchived) return WeatherProcessResult.Failure("Cannot process weather intelligence for an archived trip.");

        var city = trip.Destination?.City;
        var country = trip.Destination?.Country;
        if (string.IsNullOrWhiteSpace(city)) return WeatherProcessResult.Failure("Trip destination city is required.");

        if (!force && trip.LastWeatherCheckAt.HasValue && trip.LastWeatherCheckAt.Value > DateTime.UtcNow.AddHours(-24))
        {
            var activeImpact = await _db.TripWeatherImpacts.AnyAsync(i => i.TripId == tripId && !i.IsResolved, ct);
            if (!activeImpact && !trip.HasWeatherImpact)
                return WeatherProcessResult.Ok(hasWeatherImpact: false);
        }

        var dailyResponse = await _weatherClient.GetDailyForecastAsync(city, country ?? "", trip.DepartDate, trip.ReturnDate, ct);
        if (dailyResponse?.Forecasts is null || dailyResponse.Forecasts.Count == 0)
            return WeatherProcessResult.Failure("No weather forecast data returned for the destination.");

        var cachedForecasts = await _db.TripWeatherForecasts
            .Where(f => f.TripId == tripId)
            .ToDictionaryAsync(f => f.ForecastDate, ct);

        var detectedDeltas = DetectDeltas(dailyResponse.Forecasts, cachedForecasts);

        UpdateForecastCache(tripId, dailyResponse.Forecasts, cachedForecasts);
        trip.LastWeatherCheckAt = DateTime.UtcNow;

        if (detectedDeltas.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
            return WeatherProcessResult.Ok(hasWeatherImpact: false);
        }

        var existingItemTitles = await _tripItemsClient.GetItemTitlesAsync(tripId, ct);

        var aiPayload = new WeatherRepackRequestPayload(
            Destination: $"{city}, {country}".Trim(',', ' '),
            Dates: $"{trip.DepartDate:MMM d} – {trip.ReturnDate:MMM d}",
            ForecastDelta: detectedDeltas,
            ExistingTripItems: existingItemTitles
        );

        var aiRepackResult = await _aiClient.WeatherRepackAsync(aiPayload, ct);
        if (aiRepackResult is null)
            return WeatherProcessResult.Failure("AI service returned empty weather repack recommendations.");

        var unresolvedImpacts = await _db.TripWeatherImpacts
            .Where(i => i.TripId == tripId && !i.IsResolved)
            .ToListAsync(ct);

        foreach (var imp in unresolvedImpacts)
        {
            imp.IsResolved = true;
            imp.ResolvedAt = DateTime.UtcNow;
        }

        var newImpact = new TripWeatherImpact
        {
            TripId = trip.Id,
            DetectedAt = DateTime.UtcNow,
            IsResolved = false,
            Highlights = aiRepackResult.Highlights ?? new List<WeatherHighlightDto>(),
            Proposals = aiRepackResult.Proposals ?? new List<WeatherProposalDto>(),
            ExistingMatchedNotice = aiRepackResult.ExistingMatchedNotice
        };

        _db.TripWeatherImpacts.Add(newImpact);
        trip.HasWeatherImpact = true;

        await _db.SaveChangesAsync(ct);

        await _notificationClient.SendAsync(new ExternalCreateNotificationRequestDto(
            UserId: trip.UserId,
            TripId: trip.Id,
            Category: "weatherAlert",
            Title: $"🌧️ {BuildWeatherAlertTitle(aiRepackResult.Highlights, city)}",
            Body: BuildWeatherAlertBody(aiRepackResult.Highlights, city),
            ActionTitle: "Review Repack",
            RelatedDestination: $"{city}, {country}".Trim(',', ' '),
            ImpactId: newImpact.Id,
            SendPush: true
        ), ct);

        return WeatherProcessResult.Ok(hasWeatherImpact: true);
    }

    public async Task<WeatherIntelligenceResponseDto> GetWeatherIntelligenceAsync(Guid tripId, CancellationToken ct = default)
    {
        var impact = await _db.TripWeatherImpacts
            .AsNoTracking()
            .Where(i => i.TripId == tripId && !i.IsResolved)
            .OrderByDescending(i => i.DetectedAt)
            .FirstOrDefaultAsync(ct);

        impact ??= await _db.TripWeatherImpacts
            .AsNoTracking()
            .Where(i => i.TripId == tripId)
            .OrderByDescending(i => i.DetectedAt)
            .FirstOrDefaultAsync(ct);

        if (impact is null)
        {
            return new WeatherIntelligenceResponseDto(
                HasActiveImpact: false,
                ImpactId: null,
                DetectedAt: null,
                Highlights: new List<WeatherHighlightDto>(),
                Proposals: new List<WeatherProposalDto>(),
                ExistingMatchedNotice: null
            );
        }

        return new WeatherIntelligenceResponseDto(
            HasActiveImpact: !impact.IsResolved,
            ImpactId: impact.Id,
            DetectedAt: impact.DetectedAt,
            Highlights: impact.Highlights ?? new List<WeatherHighlightDto>(),
            Proposals: impact.Proposals ?? new List<WeatherProposalDto>(),
            ExistingMatchedNotice: impact.ExistingMatchedNotice
        );
    }

    public async Task<ApplyWeatherAdjustmentsResponseDto> ApplyWeatherAdjustmentsAsync(Guid tripId, ApplyWeatherAdjustmentsRequestDto request, CancellationToken ct = default)
    {
        var impact = await _db.TripWeatherImpacts
            .FirstOrDefaultAsync(i => i.Id == request.ImpactId && i.TripId == tripId, ct);

        if (impact is null)
            return new ApplyWeatherAdjustmentsResponseDto(false, 0, "Weather impact record not found.", new List<AddedTripItemDto>());

        var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == tripId, ct);
        if (trip is not null) trip.HasWeatherImpact = false;

        var selectedSet = new HashSet<Guid>(request.SelectedItemIds);
        var proposalsToApply = impact.Proposals.Where(p => selectedSet.Contains(p.Id)).ToList();
        var addedItemsList = new List<AddedTripItemDto>();

        if (proposalsToApply.Count > 0)
        {
            var payload = new ExternalBulkCreateTripItemsPayload(
                proposalsToApply.Select(p => new ExternalCreateTripItemDto(
                    Section: p.Section,
                    Title: p.Title,
                    Quantity: p.Quantity > 0 ? p.Quantity : 1,
                    Tag: p.Tag
                )).ToList()
            );

            var createdItems = await _tripItemsClient.BulkCreateAsync(tripId, payload, ct);
            addedItemsList = createdItems.Select(ci => new AddedTripItemDto(
                Id: ci.Id,
                Section: ci.Section,
                Title: ci.Title,
                Quantity: ci.Quantity,
                Tag: ci.Tag ?? "recommended",
                IsPacked: ci.IsTaken
            )).ToList();
        }

        impact.IsResolved = true;
        impact.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ApplyWeatherAdjustmentsResponseDto(
            Success: true,
            AddedCount: addedItemsList.Count,
            Message: $"Applied {addedItemsList.Count} weather adjustments. Packed items remain unchanged.",
            AddedItems: addedItemsList
        );
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static List<WeatherRepackForecastDeltaItemDto> DetectDeltas(
        List<ExternalDailyForecastItemDto> forecasts,
        Dictionary<DateOnly, TripWeatherForecastCache> cached)
    {
        var deltas = new List<WeatherRepackForecastDeltaItemDto>();

        foreach (var forecast in forecasts)
        {
            cached.TryGetValue(forecast.Date, out var prev);

            var isRainSpike = forecast.RainProbability >= 50 && (prev == null || prev.RainProbability < 30);
            var isColdFront = forecast.LowTemp <= 10 || (prev != null && prev.LowTemp - forecast.LowTemp >= 5);
            var isHeatwave = forecast.HighTemp >= 32 || (prev != null && forecast.HighTemp - prev.HighTemp >= 5);
            var isHighUv = forecast.UvIndex >= 6;
            var isHighWind = forecast.WindSpeed >= 45 || forecast.Condition.Contains("storm", StringComparison.OrdinalIgnoreCase);

            if (!isRainSpike && !isColdFront && !isHeatwave && !isHighUv && !isHighWind) continue;

            var notes = new List<string>();
            if (isRainSpike) notes.Add(prev != null
                ? $"Rain probability increased from {prev.RainProbability}% to {forecast.RainProbability}%"
                : $"High rain probability ({forecast.RainProbability}%)");
            if (isColdFront) notes.Add(prev != null
                ? $"Night temperature dropped from {prev.LowTemp}°C to {forecast.LowTemp}°C"
                : $"Low nighttime temperature ({forecast.LowTemp}°C)");
            if (isHeatwave) notes.Add($"Daytime temperature reached {forecast.HighTemp}°C");
            if (isHighUv) notes.Add($"High midday UV index ({forecast.UvIndex})");
            if (isHighWind) notes.Add($"High wind speed forecast ({forecast.WindSpeed} km/h)");

            deltas.Add(new WeatherRepackForecastDeltaItemDto(
                Date: $"{forecast.Date:yyyy-MM-dd} ({forecast.Date.DayOfWeek})",
                RainProbability: forecast.RainProbability,
                NightTemp: forecast.LowTemp,
                DayTemp: forecast.HighTemp,
                UvIndex: forecast.UvIndex,
                WindSpeed: forecast.WindSpeed,
                Condition: forecast.Condition,
                Delta: string.Join("; ", notes)
            ));
        }

        return deltas;
    }

    private void UpdateForecastCache(Guid tripId, List<ExternalDailyForecastItemDto> forecasts, Dictionary<DateOnly, TripWeatherForecastCache> existing)
    {
        foreach (var f in forecasts)
        {
            if (existing.TryGetValue(f.Date, out var cached))
            {
                cached.HighTemp = f.HighTemp;
                cached.LowTemp = f.LowTemp;
                cached.Condition = f.Condition;
                cached.IconName = f.IconName;
                cached.RainProbability = f.RainProbability;
                cached.UvIndex = f.UvIndex;
                cached.WindSpeed = f.WindSpeed;
                cached.FetchedAt = DateTime.UtcNow;
            }
            else
            {
                _db.TripWeatherForecasts.Add(new TripWeatherForecastCache
                {
                    TripId = tripId,
                    ForecastDate = f.Date,
                    HighTemp = f.HighTemp,
                    LowTemp = f.LowTemp,
                    Condition = f.Condition,
                    IconName = f.IconName,
                    RainProbability = f.RainProbability,
                    UvIndex = f.UvIndex,
                    WindSpeed = f.WindSpeed,
                    FetchedAt = DateTime.UtcNow
                });
            }
        }
    }

    private static string BuildWeatherAlertTitle(List<WeatherHighlightDto>? highlights, string city)
    {
        var primary = highlights?.FirstOrDefault();
        return primary is { Title: { Length: > 0 } }
            ? $"Forecast Update: {primary.Title}"
            : $"Weather Alert for {city}";
    }

    private static string BuildWeatherAlertBody(List<WeatherHighlightDto>? highlights, string city)
    {
        var primary = highlights?.FirstOrDefault();
        return primary is { Detail: { Length: > 0 } }
            ? $"{city}: {primary.Detail}"
            : $"Significant weather changes forecast for your upcoming trip to {city}. Review adjustments for your packing list.";
    }
}
