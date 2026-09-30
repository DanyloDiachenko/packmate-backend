using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using WeatherService.DTOs;

namespace WeatherService.Services;

public class CachedWeatherService : IWeatherProviderService
{
    private readonly IWeatherProviderService _inner;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _config;

    public CachedWeatherService(IWeatherProviderService inner, IDistributedCache cache, IConfiguration config)
    {
        _inner = inner;
        _cache = cache;
        _config = config;
    }

    public async Task<WeatherResponse?> GetWeatherAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default)
    {
        var dateKey = departDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cacheKey = $"weather:{city.ToLower().Trim()}:{dateKey}";

        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<WeatherResponse>(cachedData);
        }

        var weather = await _inner.GetWeatherAsync(city, country, departDate, returnDate, ct);
        if (weather == null)
        {
            return null;
        }

        var hours = int.Parse(_config["Redis:CacheDurationHours"] ?? "4");
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(hours)
        };

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(weather), cacheOptions, ct);

        return weather;
    }
}