using TripService.DTOs;

namespace TripService.Infrastructure;

public interface IAiServiceClient
{
    Task<WeatherRepackResponsePayload?> WeatherRepackAsync(
        WeatherRepackRequestPayload payload,
        CancellationToken ct = default);
}

public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _http;

    public AiServiceClient(HttpClient http) => _http = http;

    public async Task<WeatherRepackResponsePayload?> WeatherRepackAsync(
        WeatherRepackRequestPayload payload,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/ai/weather-repack", payload, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<WeatherRepackResponsePayload>(cancellationToken: ct)
            : null;
    }
}
