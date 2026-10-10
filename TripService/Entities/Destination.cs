namespace TripService.Entities;










public record Destination(
    string Country,
    string CountryFlag,
    string City,
    string? BannerImage,
    string? WeatherToday = null,
    string? CurrentSeason = null
);