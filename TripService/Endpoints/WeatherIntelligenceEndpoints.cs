using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TripService.Data;
using TripService.DTOs;
using TripService.Extensions;
using TripService.Services;

namespace TripService.Endpoints;

public static class WeatherIntelligenceEndpoints
{
    public static RouteGroupBuilder MapWeatherIntelligenceEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Weather Intelligence");

        group.MapGet("/{tripId:guid}/weather-intelligence", async (
            Guid tripId,
            ClaimsPrincipal userClaims,
            TripDbContext db,
            IWeatherIntelligenceService weatherService,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var tripExists = await db.Trips.AnyAsync(t => t.Id == tripId && t.UserId == userId, ct);
            if (!tripExists)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            var result = await weatherService.GetWeatherIntelligenceAsync(tripId, ct);
            return Results.Ok(result);
        })
        .WithName("GetWeatherIntelligence")
        .WithSummary("Get active weather impact and repack proposals for a trip")
        .WithDescription("Returns current weather highlights, item proposals, and weather impact status.")
        .Produces<WeatherIntelligenceResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{tripId:guid}/weather-intelligence/apply", async (
            Guid tripId,
            ApplyWeatherAdjustmentsRequestDto request,
            ClaimsPrincipal userClaims,
            TripDbContext db,
            IWeatherIntelligenceService weatherService,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var tripExists = await db.Trips.AnyAsync(t => t.Id == tripId && t.UserId == userId, ct);
            if (!tripExists)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            var result = await weatherService.ApplyWeatherAdjustmentsAsync(tripId, request, ct);
            if (!result.Success && result.Message == "Weather impact record not found.")
            {
                return Results.NotFound(new ErrorResponse(result.Message));
            }

            return Results.Ok(result);
        })
        .WithName("ApplyWeatherAdjustments")
        .WithSummary("Apply selected weather repack proposals to trip packing list")
        .WithDescription("Inserts selected proposal items into TripItems without resetting is_packed for existing items, and marks impact as resolved.")
        .Produces<ApplyWeatherAdjustmentsResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{tripId:guid}/weather-intelligence/recheck", async (
            Guid tripId,
            ClaimsPrincipal userClaims,
            TripDbContext db,
            IWeatherIntelligenceService weatherService,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var tripExists = await db.Trips.AnyAsync(t => t.Id == tripId && t.UserId == userId, ct);
            if (!tripExists)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            var processResult = await weatherService.CheckAndProcessTripAsync(tripId, force: true, ct: ct);
            if (!processResult.Success && !string.IsNullOrEmpty(processResult.ErrorMessage))
            {
                return Results.BadRequest(new ErrorResponse(processResult.ErrorMessage));
            }

            var intelligence = await weatherService.GetWeatherIntelligenceAsync(tripId, ct);
            return Results.Ok(intelligence);
        })
        .WithName("RecheckWeatherIntelligence")
        .WithSummary("Manually re-check weather and re-compute packing impact")
        .WithDescription("Bypasses 24h cooldown, refreshes weather forecast, and recalculates weather delta and repack suggestions.")
        .Produces<WeatherIntelligenceResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return group;
    }
}
