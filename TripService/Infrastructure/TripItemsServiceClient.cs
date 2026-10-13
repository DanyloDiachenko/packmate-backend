using TripService.DTOs;

namespace TripService.Infrastructure;

public interface ITripItemsServiceClient
{
    Task<List<string>> GetItemTitlesAsync(Guid tripId, CancellationToken ct = default);
    Task<List<ExternalTripItemDto>> BulkCreateAsync(Guid tripId, ExternalBulkCreateTripItemsPayload payload, CancellationToken ct = default);
}

public class TripItemsServiceClient : ITripItemsServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<TripItemsServiceClient> _logger;

    public TripItemsServiceClient(HttpClient http, ILogger<TripItemsServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<string>> GetItemTitlesAsync(Guid tripId, CancellationToken ct = default)
    {
        try
        {
            var groups = await _http.GetFromJsonAsync<List<ExternalTripSectionGroupDto>>($"/trip-items/{tripId}", ct);
            return groups?.SelectMany(g => g.Items).Select(i => i.Title).Distinct().ToList() ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch trip items for {TripId}", tripId);
            return new();
        }
    }

    public async Task<List<ExternalTripItemDto>> BulkCreateAsync(Guid tripId, ExternalBulkCreateTripItemsPayload payload, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"/trip-items/{tripId}/bulk", payload, ct);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<ExternalTripItemDto>>(cancellationToken: ct) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk-create trip items for {TripId}", tripId);
            return new();
        }
    }
}
