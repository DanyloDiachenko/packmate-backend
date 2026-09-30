

using WeatherService.DTOs;
using WeatherService.Services;

namespace WeatherService.Endpoints;

public static class WeatherEndpoints
{
    public static RouteGroupBuilder MapWeatherEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Weather");

        group.MapGet("/", async (
            string city,
            string country,
            DateOnly departDate,
            DateOnly returnDate,
            IWeatherProviderService weatherService,
            CancellationToken ct
        ) =>
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return Results.BadRequest(new ErrorResponse("City query parameter is required"));
            }

            var result = await weatherService.GetWeatherAsync(city, country, departDate, returnDate, ct);
            if (result == null)
            {
                return Results.NotFound(new ErrorResponse($"Weather data not found for city '{city}'"));
            }
            return Results.Ok(result);
        })
        .WithName("GetWeather")
        .WithSummary("Get destination weather and climate summary")
        .WithDescription("Fetches weather forecast or seasonal climate averages for the given city and country across the specified trip departure and return dates.")
        .Produces<WeatherResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return group;
    }
}