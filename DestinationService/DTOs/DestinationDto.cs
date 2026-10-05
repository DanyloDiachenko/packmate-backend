namespace DestinationService.DTOs;

/// <summary>
/// Destination details including location information, country flag, and scenic banner image.
/// </summary>
/// <param name="Country">Country name (e.g. France, Japan, United States).</param>
/// <param name="City">City name (e.g. Paris, Tokyo, New York).</param>
/// <param name="CountryFlag">Country flag emoji (e.g. 🇫🇷, 🇯🇵, 🇺🇸).</param>
/// <param name="BannerImage">High-resolution banner/landscape image URL for the city or country.</param>
public record DestinationDto(
    string Country,
    string City,
    string CountryFlag,
    string? BannerImage
);

