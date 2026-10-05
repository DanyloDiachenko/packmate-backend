using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using DestinationService.DTOs;

namespace DestinationService.Services;

public class DestinationServiceImpl : IDestinationService
{
    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DestinationServiceImpl> _logger;

    public DestinationServiceImpl(
        HttpClient http,
        IDistributedCache cache,
        ILogger<DestinationServiceImpl> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DestinationDto>> SearchDestinationsAsync(string? query, int limit = 20, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 20;
        if (limit > 50) limit = 50;

        var normalizedQuery = query?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return CuratedDestinations.Take(limit).ToList();
        }

        var cacheKey = $"dest_search_{normalizedQuery}_{limit}";
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var cachedResults = JsonSerializer.Deserialize<List<DestinationDto>>(cached);
                if (cachedResults != null && cachedResults.Count > 0)
                {
                    return cachedResults;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached search results for key {CacheKey}", cacheKey);
            }
        }

        var results = new List<DestinationDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var curatedMatches = CuratedDestinations
            .Where(d => d.City.ToLowerInvariant().Contains(normalizedQuery) ||
                        d.Country.ToLowerInvariant().Contains(normalizedQuery))
            .ToList();

        foreach (var match in curatedMatches)
        {
            var key = $"{match.City}:{match.Country}";
            if (seen.Add(key))
            {
                results.Add(match);
            }
            if (results.Count >= limit) break;
        }

        if (results.Count < limit)
        {
            try
            {
                var geocodingUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(normalizedQuery)}&count={limit}&language=en&format=json";
                var geoResponse = await _http.GetFromJsonAsync<GeocodingApiResponse>(geocodingUrl, ct);

                if (geoResponse?.Results != null)
                {
                    foreach (var geo in geoResponse.Results)
                    {
                        var city = geo.Name;
                        var country = geo.Country ?? geo.Name;
                        var key = $"{city}:{country}";

                        if (!seen.Add(key)) continue;

                        var flag = GetCountryFlag(geo.CountryCode);
                        var banner = await FetchBannerImageAsync(city, country, ct);

                        results.Add(new DestinationDto(
                            Country: country,
                            City: city,
                            CountryFlag: flag,
                            BannerImage: banner
                        ));

                        if (results.Count >= limit) break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying geocoding API for query '{Query}'", normalizedQuery);
            }
        }

        try
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(results), cacheOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache search results for {CacheKey}", cacheKey);
        }

        return results;
    }

    public async Task<string?> FetchBannerImageAsync(string city, string country, CancellationToken ct)
    {
        var cacheKey = $"banner_{city}_{country}".ToLowerInvariant();
        var cachedBanner = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedBanner))
        {
            return cachedBanner;
        }

        string? bannerUrl = null;

        try
        {
            var wikiUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(city)}";
            var wikiResponse = await _http.GetFromJsonAsync<WikiSummaryResponse>(wikiUrl, ct);

            bannerUrl = wikiResponse?.OriginalImage?.Source ?? wikiResponse?.Thumbnail?.Source;
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(bannerUrl))
        {
            try
            {
                var wikiUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(country)}";
                var wikiResponse = await _http.GetFromJsonAsync<WikiSummaryResponse>(wikiUrl, ct);

                bannerUrl = wikiResponse?.OriginalImage?.Source ?? wikiResponse?.Thumbnail?.Source;
            }
            catch
            {
            }
        }

        if (string.IsNullOrEmpty(bannerUrl))
        {
            bannerUrl = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?auto=format&fit=crop&w=1600&q=80";
        }

        try
        {
            await _cache.SetStringAsync(cacheKey, bannerUrl, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            }, ct);
        }
        catch
        {
        }

        return bannerUrl;
    }

    public static string GetCountryFlag(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            return "🌍";

        countryCode = countryCode.ToUpperInvariant();
        if (countryCode[0] < 'A' || countryCode[0] > 'Z' || countryCode[1] < 'A' || countryCode[1] > 'Z')
            return "🌍";

        return char.ConvertFromUtf32(0x1F1E6 + countryCode[0] - 'A') +
               char.ConvertFromUtf32(0x1F1E6 + countryCode[1] - 'A');
    }

    private static readonly List<DestinationDto> CuratedDestinations = new()
    {
        new("France", "Paris", "🇫🇷", "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?auto=format&fit=crop&w=1600&q=80"),
        new("France", "Nice", "🇫🇷", "https://images.unsplash.com/photo-1533105079780-92b9be482077?auto=format&fit=crop&w=1600&q=80"),
        new("France", "Lyon", "🇫🇷", "https://images.unsplash.com/photo-1524850011238-e3d235c7d4c9?auto=format&fit=crop&w=1600&q=80"),

        new("Italy", "Rome", "🇮🇹", "https://images.unsplash.com/photo-1552832230-c0197dd311b5?auto=format&fit=crop&w=1600&q=80"),
        new("Italy", "Florence", "🇮🇹", "https://images.unsplash.com/photo-1543429776-2782fc8e1acd?auto=format&fit=crop&w=1600&q=80"),
        new("Italy", "Venice", "🇮🇹", "https://images.unsplash.com/photo-1514890547357-a9ee288728e0?auto=format&fit=crop&w=1600&q=80"),
        new("Italy", "Milan", "🇮🇹", "https://images.unsplash.com/photo-1513581166391-887a96ddeafd?auto=format&fit=crop&w=1600&q=80"),

        new("Japan", "Tokyo", "🇯🇵", "https://images.unsplash.com/photo-1503899036084-c55cdd92da26?auto=format&fit=crop&w=1600&q=80"),
        new("Japan", "Kyoto", "🇯🇵", "https://images.unsplash.com/photo-1493976040374-85c8e12f0c0e?auto=format&fit=crop&w=1600&q=80"),
        new("Japan", "Osaka", "🇯🇵", "https://images.unsplash.com/photo-1590559899731-a382839e5549?auto=format&fit=crop&w=1600&q=80"),

        new("Spain", "Barcelona", "🇪🇸", "https://images.unsplash.com/photo-1583422409516-2895a77efded?auto=format&fit=crop&w=1600&q=80"),
        new("Spain", "Madrid", "🇪🇸", "https://images.unsplash.com/photo-1539037116277-4db20889f2d4?auto=format&fit=crop&w=1600&q=80"),

        new("United Kingdom", "London", "🇬🇧", "https://images.unsplash.com/photo-1513635269975-59663e0ac1ad?auto=format&fit=crop&w=1600&q=80"),
        new("United Kingdom", "Edinburgh", "🇬🇧", "https://images.unsplash.com/photo-1506377585622-be77d028ec7f?auto=format&fit=crop&w=1600&q=80"),

        new("United States", "New York", "🇺🇸", "https://images.unsplash.com/photo-1496442226666-8d4d0e62e6e9?auto=format&fit=crop&w=1600&q=80"),
        new("United States", "San Francisco", "🇺🇸", "https://images.unsplash.com/photo-1501594907352-04cda38ebc29?auto=format&fit=crop&w=1600&q=80"),
        new("United States", "Los Angeles", "🇺🇸", "https://images.unsplash.com/photo-1580655653885-65763b2597d0?auto=format&fit=crop&w=1600&q=80"),
        new("United States", "Honolulu", "🇺🇸", "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=1600&q=80"),

        new("Germany", "Berlin", "🇩🇪", "https://images.unsplash.com/photo-1560969184-10fe8719e047?auto=format&fit=crop&w=1600&q=80"),
        new("Germany", "Munich", "🇩🇪", "https://images.unsplash.com/photo-1595867818082-083862f3d630?auto=format&fit=crop&w=1600&q=80"),

        new("Netherlands", "Amsterdam", "🇳🇱", "https://images.unsplash.com/photo-1512470876302-972faa2aa9a4?auto=format&fit=crop&w=1600&q=80"),

        new("Austria", "Vienna", "🇦🇹", "https://images.unsplash.com/photo-1516550893923-42d28e5677af?auto=format&fit=crop&w=1600&q=80"),

        new("Czech Republic", "Prague", "🇨🇿", "https://images.unsplash.com/photo-1541849546-216549ae216d?auto=format&fit=crop&w=1600&q=80"),

        new("Greece", "Athens", "🇬🇷", "https://images.unsplash.com/photo-1565008447742-97f6f38c985c?auto=format&fit=crop&w=1600&q=80"),
        new("Greece", "Santorini", "🇬🇷", "https://images.unsplash.com/photo-1570077188670-e3a8d69ac5ff?auto=format&fit=crop&w=1600&q=80"),

        new("Portugal", "Lisbon", "🇵🇹", "https://images.unsplash.com/photo-1509840841025-9088ba78a826?auto=format&fit=crop&w=1600&q=80"),
        new("Portugal", "Porto", "🇵🇹", "https://images.unsplash.com/photo-1555881400-74d7acaacd81?auto=format&fit=crop&w=1600&q=80"),

        new("United Arab Emirates", "Dubai", "🇦🇪", "https://images.unsplash.com/photo-1512453979798-5ea266f8880c?auto=format&fit=crop&w=1600&q=80"),

        new("Singapore", "Singapore", "🇸🇬", "https://images.unsplash.com/photo-1525625293386-3f8f99389edd?auto=format&fit=crop&w=1600&q=80"),

        new("Thailand", "Bangkok", "🇹🇭", "https://images.unsplash.com/photo-1508009603885-50cf7c579365?auto=format&fit=crop&w=1600&q=80"),
        new("Thailand", "Phuket", "🇹🇭", "https://images.unsplash.com/photo-1589394815804-964ed0be2eb5?auto=format&fit=crop&w=1600&q=80"),

        new("Indonesia", "Bali", "🇮🇩", "https://images.unsplash.com/photo-1537996194471-e657df975ab4?auto=format&fit=crop&w=1600&q=80"),

        new("Australia", "Sydney", "🇦🇺", "https://images.unsplash.com/photo-1506973035872-a4ec16b8e8d9?auto=format&fit=crop&w=1600&q=80"),
        new("Australia", "Melbourne", "🇦🇺", "https://images.unsplash.com/photo-1514395462725-fb4566210144?auto=format&fit=crop&w=1600&q=80"),

        new("Canada", "Vancouver", "🇨🇦", "https://images.unsplash.com/photo-1559511260-66a65e09b245?auto=format&fit=crop&w=1600&q=80"),
        new("Canada", "Toronto", "🇨🇦", "https://images.unsplash.com/photo-1517090504586-fde19ea6066f?auto=format&fit=crop&w=1600&q=80"),

        new("Switzerland", "Zurich", "🇨🇭", "https://images.unsplash.com/photo-1515488764276-beab7607c1e6?auto=format&fit=crop&w=1600&q=80"),

        new("Iceland", "Reykjavik", "🇮🇸", "https://images.unsplash.com/photo-1504893524553-b855bce32c67?auto=format&fit=crop&w=1600&q=80"),

        new("Ukraine", "Kyiv", "🇺🇦", "https://images.unsplash.com/photo-1561542320-9a18ce34de45?auto=format&fit=crop&w=1600&q=80"),
        new("Ukraine", "Lviv", "🇺🇦", "https://images.unsplash.com/photo-1596701062351-8c2c14d1fdd0?auto=format&fit=crop&w=1600&q=80"),

        new("Brazil", "Rio de Janeiro", "🇧🇷", "https://images.unsplash.com/photo-1483729558449-99ef09a8c325?auto=format&fit=crop&w=1600&q=80")
    };
}


