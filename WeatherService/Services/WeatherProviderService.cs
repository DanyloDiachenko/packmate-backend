using System.Text.Json;
using System.Text.Json.Serialization;
using WeatherService.Common;
using WeatherService.DTOs;

namespace WeatherService.Services;

public interface IWeatherProviderService
{
    Task<WeatherResponse?> GetWeatherAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default);
}

public class OpenMeteoWeatherService : IWeatherProviderService
{
    private readonly HttpClient _http;

    public OpenMeteoWeatherService(HttpClient http)
    {
        _http = http;
    }

    public async Task<WeatherResponse?> GetWeatherAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default)
    {
        var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
        var geoResponse = await _http.GetFromJsonAsync<GeocodingApiResponse>(geoUrl, ct);

        var location = geoResponse?.Results?.FirstOrDefault();
        if (location == null)
        {
            return null;
        }

        var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto";
        var weatherData = await _http.GetFromJsonAsync<OpenMeteoApiResponse>(weatherUrl, ct);

        var currentTemp = weatherData?.Current?.Temperature2m ?? 20.0;
        var maxRainChance = weatherData?.Daily?.PrecipitationProbabilityMax?.DefaultIfEmpty(0).Max() ?? 0;
        var hasRain = maxRainChance > 40;

        var condition = DecodeWeatherCode(weatherData?.Current?.WeatherCode ?? 0);
        var targetDate = departDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow;
        var season = SeasonHelper.GetSeason(targetDate);

        return new WeatherResponse(
            City: location.Name,
            Country: location.Country ?? country ?? "Unknown",
            WeatherToday: $"{Math.Round(currentTemp)}°C, {condition}",
            CurrentSeason: season,
            AverageTemperatureC: Math.Round(currentTemp, 1),
            HasRainRisk: hasRain,
            ConditionDescription: hasRain ? $"{condition} (rain probability: {maxRainChance}%)" : condition,
            FetchedAt: DateTime.UtcNow
        );
    }

    private static string DecodeWeatherCode(int code) => code switch
    {
        0 => "Clear sky",
        1 or 2 or 3 => "Partly cloudy",
        45 or 48 => "Foggy",
        51 or 53 or 55 => "Drizzle",
        61 or 63 or 65 => "Rain",
        71 or 73 or 75 => "Snow",
        80 or 81 or 82 => "Heavy Rain showers",
        95 => "Thunderstorm",
        _ => "Fair"
    };
}

public record GeocodingApiResponse([property: JsonPropertyName("results")] List<GeocodingResult>? Results);
public record GeocodingResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude
);

public record OpenMeteoApiResponse(
    [property: JsonPropertyName("current")] CurrentWeather? Current,
    [property: JsonPropertyName("daily")] DailyWeather? Daily
);
public record CurrentWeather(
    [property: JsonPropertyName("temperature_2m")] double Temperature2m,
    [property: JsonPropertyName("weather_code")] int WeatherCode
);
public record DailyWeather(
    [property: JsonPropertyName("precipitation_probability_max")] List<int>? PrecipitationProbabilityMax
);