using TripService.DTOs;

namespace TripService.Infrastructure;

public interface IWeatherServiceClient
{
    Task<ExternalTripDailyForecastResponse?> GetDailyForecastAsync(
        string city,
        string country,
        DateOnly departDate,
        DateOnly returnDate,
        CancellationToken ct = default);
}

public class WeatherServiceClient : IWeatherServiceClient
{
    private readonly HttpClient _http;

    public WeatherServiceClient(HttpClient http) => _http = http;

    public Task<ExternalTripDailyForecastResponse?> GetDailyForecastAsync(
        string city,
        string country,
        DateOnly departDate,
        DateOnly returnDate,
        CancellationToken ct = default)
    {
        var url = $"/daily?city={Uri.EscapeDataString(city)}" +
                  $"&country={Uri.EscapeDataString(country)}" +
                  $"&departDate={departDate:yyyy-MM-dd}" +
                  $"&returnDate={returnDate:yyyy-MM-dd}";
        return _http.GetFromJsonAsync<ExternalTripDailyForecastResponse>(url, ct);
    }
}
