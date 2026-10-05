namespace TripService.Entities;

/// <summary>
/// Destination location and environmental metadata for a trip.
/// </summary>
/// <param name="Country">Country name (e.g. France, Japan).</param>
/// <param name="CountryFlag">Country flag emoji or code (e.g. 🇫🇷, 🇯🇵).</param>
/// <param name="City">City name (e.g. Paris, Tokyo).</param>
/// <param name="BannerImage">Optional URL of a high-resolution scenic banner image for the destination.</param>
/// <param name="WeatherToday">Optional current weather description.</param>
/// <param name="CurrentSeason">Optional season name (e.g. Summer, Winter, Spring, Autumn).</param>
public record Destination(
    string Country,
    string CountryFlag,
    string City,
    string? BannerImage,
    string? WeatherToday = null,
    string? CurrentSeason = null
);