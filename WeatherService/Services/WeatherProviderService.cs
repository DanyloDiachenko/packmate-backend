using System.Text.Json.Serialization;
using WeatherService.Common;
using WeatherService.DTOs;

namespace WeatherService.Services;

public interface IWeatherProviderService
{
    Task<WeatherResponse?> GetWeatherAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default);
    Task<TripDailyForecastResponse?> GetDailyForecastAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default);
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
        var location = await GetLocationAsync(city, ct);
        if (location == null) return null;

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

    public async Task<TripDailyForecastResponse?> GetDailyForecastAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default)
    {
        var location = await GetLocationAsync(city, ct);
        if (location == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = departDate ?? today;
        var endDate = returnDate ?? startDate.AddDays(7);
        if (endDate < startDate)
        {
            endDate = startDate;
        }

        var pastDays = Math.Min(92, Math.Max(0, today.DayNumber - startDate.DayNumber));
        var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,uv_index_max,wind_speed_10m_max&timezone=auto&forecast_days=16{(pastDays > 0 ? $"&past_days={pastDays}" : "")}";
        var weatherData = await _http.GetFromJsonAsync<OpenMeteoApiResponse>(weatherUrl, ct);

        var times = weatherData?.Daily?.Time ?? new List<string>();
        var codes = weatherData?.Daily?.WeatherCode ?? new List<int>();
        var maxTemps = weatherData?.Daily?.Temperature2mMax ?? new List<double>();
        var minTemps = weatherData?.Daily?.Temperature2mMin ?? new List<double>();
        var rainProbs = weatherData?.Daily?.PrecipitationProbabilityMax ?? new List<int>();
        var uvIndices = weatherData?.Daily?.UvIndexMax ?? new List<double>();
        var windSpeeds = weatherData?.Daily?.WindSpeed10mMax ?? new List<double>();

        var forecastMap = new Dictionary<DateOnly, DailyForecastItemDto>();
        for (var i = 0; i < times.Count; i++)
        {
            if (DateOnly.TryParse(times[i], out var date))
            {
                var code = i < codes.Count ? codes[i] : 0;
                var high = i < maxTemps.Count ? (int)Math.Round(maxTemps[i]) : 20;
                var low = i < minTemps.Count ? (int)Math.Round(minTemps[i]) : 12;
                var rain = i < rainProbs.Count ? rainProbs[i] : 0;
                var uv = i < uvIndices.Count ? (int)Math.Round(uvIndices[i]) : 0;
                var wind = i < windSpeeds.Count ? (int)Math.Round(windSpeeds[i]) : 0;

                forecastMap[date] = new DailyForecastItemDto(
                    Date: date,
                    DayOfWeek: date.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture),
                    HighTemp: high,
                    LowTemp: low,
                    Condition: DecodeWeatherCode(code),
                    IconName: MapWeatherIcon(code),
                    RainProbability: rain,
                    UvIndex: uv,
                    WindSpeed: wind
                );
            }
        }

        var dailyList = new List<DailyForecastItemDto>();
        DailyForecastItemDto? fallbackItem = forecastMap.Values.LastOrDefault();

        for (var curr = startDate; curr <= endDate; curr = curr.AddDays(1))
        {
            if (forecastMap.TryGetValue(curr, out var item))
            {
                dailyList.Add(item);
                fallbackItem = item;
            }
            else
            {
                var high = fallbackItem?.HighTemp ?? 20;
                var low = fallbackItem?.LowTemp ?? 12;
                var cond = fallbackItem?.Condition ?? "Partly cloudy";
                var icon = fallbackItem?.IconName ?? "cloud.sun.fill";
                var rain = fallbackItem?.RainProbability ?? 10;
                var uv = fallbackItem?.UvIndex ?? 4;
                var wind = fallbackItem?.WindSpeed ?? 10;

                dailyList.Add(new DailyForecastItemDto(
                    Date: curr,
                    DayOfWeek: curr.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture),
                    HighTemp: high,
                    LowTemp: low,
                    Condition: cond,
                    IconName: icon,
                    RainProbability: rain,
                    UvIndex: uv,
                    WindSpeed: wind
                ));
            }
        }

        return new TripDailyForecastResponse(
            City: location.Name,
            Country: location.Country ?? country ?? "Unknown",
            Forecasts: dailyList
        );
    }

    private async Task<GeocodingResult?> GetLocationAsync(string city, CancellationToken ct)
    {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
        var response = await _http.GetFromJsonAsync<GeocodingApiResponse>(url, ct);
        return response?.Results?.FirstOrDefault();
    }

    private static string MapWeatherIcon(int code) => code switch
    {
        0 => "sun.max.fill",
        1 or 2 => "cloud.sun.fill",
        3 => "cloud.fill",
        45 or 48 => "cloud.fog.fill",
        51 or 53 or 55 => "cloud.drizzle.fill",
        61 or 63 or 65 => "cloud.rain.fill",
        71 or 73 or 75 => "snowflake",
        80 or 81 or 82 => "cloud.heavyrain.fill",
        95 or 96 or 99 => "cloud.bolt.rain.fill",
        _ => "cloud.sun.fill"
    };

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
    [property: JsonPropertyName("time")] List<string>? Time,
    [property: JsonPropertyName("weather_code")] List<int>? WeatherCode,
    [property: JsonPropertyName("temperature_2m_max")] List<double>? Temperature2mMax,
    [property: JsonPropertyName("temperature_2m_min")] List<double>? Temperature2mMin,
    [property: JsonPropertyName("precipitation_probability_max")] List<int>? PrecipitationProbabilityMax,
    [property: JsonPropertyName("uv_index_max")] List<double>? UvIndexMax,
    [property: JsonPropertyName("wind_speed_10m_max")] List<double>? WindSpeed10mMax
);