using System;

namespace WeatherService.DTOs;








public record WeatherRequest(
    string City,
    string Country,
    DateOnly DepartDate,
    DateOnly ReturnDate
);












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





public record ErrorResponse(string Message);

