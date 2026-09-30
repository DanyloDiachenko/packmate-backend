using System;

namespace WeatherService.DTOs;

/// <summary>
/// Query parameters for fetching weather data.
/// </summary>
/// <param name="City">Target city name (e.g. Rome, Tokyo).</param>
/// <param name="Country">Target country name (e.g. Italy, Japan).</param>
/// <param name="DepartDate">Trip departure date in YYYY-MM-DD format.</param>
/// <param name="ReturnDate">Trip return date in YYYY-MM-DD format.</param>
public record WeatherRequest(
    string City,
    string Country,
    DateOnly DepartDate,
    DateOnly ReturnDate
);

/// <summary>
/// Weather forecast and historical climate conditions for a destination.
/// </summary>
/// <param name="City">City name.</param>
/// <param name="Country">Country name.</param>
/// <param name="WeatherToday">General weather condition today (e.g. Sunny, Rain, Cloudy).</param>
/// <param name="CurrentSeason">Identified season (e.g. Summer, Winter, Spring, Autumn).</param>
/// <param name="AverageTemperatureC">Average expected temperature in degrees Celsius.</param>
/// <param name="HasRainRisk">Whether there is a notable precipitation/rain risk.</param>
/// <param name="ConditionDescription">Detailed description of expected weather conditions.</param>
/// <param name="FetchedAt">Timestamp when weather data was retrieved or cached.</param>
public record WeatherResponse
(
    string City,
    string Country,
    string WeatherToday,
    string CurrentSeason,
    double AverageTemperatureC,
    bool HasRainRisk,
    string ConditionDescription,
    DateTime FetchedAt
);

/// <summary>
/// Standard error response containing a descriptive error message.
/// </summary>
/// <param name="Message">Descriptive error message.</param>
public record ErrorResponse(string Message);

