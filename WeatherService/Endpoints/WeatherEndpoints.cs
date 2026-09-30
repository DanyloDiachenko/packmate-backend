

using WeatherService.Services;

namespace WeatherService.Endpoints;

public static class WeatherEndpoints
{
    public static RouteGroupBuilder MapWeatherEndpoints(this RouteGroupBuilder group)
    {
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
                return Results.BadRequest(new { message = "City query parameter is required" });
            }

            var result = await weatherService.GetWeatherAsync(city, country, departDate, returnDate, ct);
            if (result == null)
            {
                return Results.NotFound(new { message = $"Weather data not found for city '{city}'" });
            }
            return Results.Ok(result);
        });

        return group;
    }
}