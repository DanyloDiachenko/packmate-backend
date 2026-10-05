using DestinationService.DTOs;
using DestinationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace DestinationService.Endpoints;

public static class DestinationEndpoints
{
    public static RouteGroupBuilder MapDestinationEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Destinations");

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] int? limit,
            IDestinationService destinationService,
            CancellationToken ct
        ) =>
        {
            var effectiveLimit = limit is > 0 ? limit.Value : 20;
            var results = await destinationService.SearchDestinationsAsync(query, effectiveLimit, ct);
            return Results.Ok(results);
        })
        .WithName("SearchDestinations")
        .WithSummary("Search destinations or list popular travel spots")
        .WithDescription("Searches destinations by city name or country name. When no query is provided, returns curated popular travel destinations.")
        .Produces<List<DestinationDto>>(StatusCodes.Status200OK);

        return group;
    }
}
