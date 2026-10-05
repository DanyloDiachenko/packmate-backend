using System.Text.Json.Serialization;

namespace DestinationService.DTOs;

public class GeocodingApiResponse
{
    [JsonPropertyName("results")]
    public List<GeocodingApiResult>? Results { get; set; }
}

public class GeocodingApiResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}

public class WikiSummaryResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("thumbnail")]
    public WikiImage? Thumbnail { get; set; }

    [JsonPropertyName("originalimage")]
    public WikiImage? OriginalImage { get; set; }
}

public class WikiImage
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
