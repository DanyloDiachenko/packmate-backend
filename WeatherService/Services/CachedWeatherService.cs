using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using WeatherService.DTOs;
using WeatherService.Settings;

namespace WeatherService.Services;

public class CachedWeatherService : IWeatherProviderService
{
    private readonly IWeatherProviderService _inner;
    private readonly IDistributedCache _cache;
    private readonly RedisSettings _settings;

    public CachedWeatherService(IWeatherProviderService inner, IDistributedCache cache, IOptions<RedisSettings> options)
    {
        _inner = inner;
        _cache = cache;
        _settings = options.Value;
    }

    public async Task<WeatherResponse?> GetWeatherAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default)
    {
        var dateKey = departDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var cacheKey = $"weather:{city.ToLower().Trim()}:{dateKey}";
        return await GetOrSetAsync(cacheKey, () => _inner.GetWeatherAsync(city, country, departDate, returnDate, ct), ct);
    }

    public async Task<TripDailyForecastResponse?> GetDailyForecastAsync(string city, string? country, DateOnly? departDate, DateOnly? returnDate, CancellationToken ct = default)
    {
        var startKey = departDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var endKey = returnDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd");
        var cacheKey = $"weather:daily:v2:{city.ToLower().Trim()}:{startKey}:{endKey}";
        return await GetOrSetAsync(cacheKey, () => _inner.GetDailyForecastAsync(city, country, departDate, returnDate, ct), ct);
    }

    private async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CancellationToken ct) where T : class
    {
        var cached = await _cache.GetStringAsync(key, ct);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<T>(cached);

        var value = await factory();
        if (value is null) return null;

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_settings.CacheDurationHours)
        }, ct);

        return value;
    }
}