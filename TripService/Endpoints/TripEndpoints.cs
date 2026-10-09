using Microsoft.AspNetCore.Mvc;
using TripService.Data;
using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TripService.Extensions;
using TripService.DTOs;
using TripService.Entities;
using TripService.Clients;

namespace TripService.Endpoints;

public static class TripEndpoints
{
    public static RouteGroupBuilder MapTripEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Trips");

        group.MapGet("/", async (
            [FromQuery] int? limit,
            [FromQuery] int? page,
            [FromQuery] int? offset,
            [FromQuery] bool? isArchived,
            ClaimsPrincipal userClaims,
            TripDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var cutoffDate = today.AddDays(-1);

            await db.Trips
                .Where(t => t.UserId == userId && !t.IsArchived && t.ReturnDate <= cutoffDate)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsArchived, true), ct);

            var baseQuery = db.Trips
                .AsNoTracking()
                .Where(t => t.UserId == userId);

            if (isArchived.HasValue)
            {
                baseQuery = baseQuery.Where(t => t.IsArchived == isArchived.Value);
            }

            var totalItems = await baseQuery.CountAsync(ct);

            IQueryable<Trip> query = baseQuery.OrderBy(t => t.DepartDate);

            var effectiveLimit = limit is > 0 ? limit.Value : (page is > 1 ? 20 : (int?)null);

            if (offset is > 0)
            {
                query = query.Skip(offset.Value);
            }
            else if (page is > 1 && effectiveLimit.HasValue)
            {
                query = query.Skip((page.Value - 1) * effectiveLimit.Value);
            }

            if (effectiveLimit.HasValue)
            {
                query = query.Take(effectiveLimit.Value);
            }

            var trips = await query
                .Select(t => new TripResponse(
                    t.Id,
                    t.UserId,
                    t.Destination,
                    t.DepartDate,
                    t.ReturnDate,
                    t.Tags,
                    t.IsArchived,
                    t.CreatedAt
                ))
                .ToListAsync(ct);

            return Results.Ok(new PaginatedTripsResponse(totalItems, trips));
        })
        .WithName("GetUserTrips")
        .WithSummary("List all trips for the authenticated user with pagination and optional archive filter")
        .WithDescription("Returns a paginated response containing total trip count and list of trips created by the authenticated user, ordered by departure date. Supports limit, page, offset, and isArchived query parameters.")
        .Produces<PaginatedTripsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal userClaims,
            TripDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var cutoffDate = today.AddDays(-1);

            var trip = await db.Trips
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

            if (trip == null)
            {
                return Results.NotFound(new ErrorResponse("Trip was not found"));
            }

            if (!trip.IsArchived && trip.ReturnDate <= cutoffDate)
            {
                trip.IsArchived = true;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new TripResponse(
                trip.Id,
                trip.UserId,
                trip.Destination,
                trip.DepartDate,
                trip.ReturnDate,
                trip.Tags,
                trip.IsArchived,
                trip.CreatedAt
            ));
        })
        .WithName("GetTripById")
        .WithSummary("Get a trip by ID")
        .WithDescription("Retrieves the details of a specific trip belonging to the authenticated user.")
        .Produces<TripResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            CreateTripRequest request,
            IValidator<CreateTripRequest> validator,
            ClaimsPrincipal userClaims,
            TripDbContext db
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var userId = userClaims.GetUserId();

            var hasOverlap = await db.Trips.AnyAsync(t =>
                t.UserId == userId &&
                (
                    (request.DepartDate < t.ReturnDate && request.ReturnDate > t.DepartDate) ||
                    (request.DepartDate == request.ReturnDate && t.DepartDate == t.ReturnDate && request.DepartDate == t.DepartDate)
                ));

            if (hasOverlap)
            {
                return Results.BadRequest(new ErrorResponse("Trip dates overlap with an existing trip. Multiple trips cannot cover the same date range."));
            }

            var trip = new Trip
            {
                UserId = userId,
                Destination = request.Destination,
                DepartDate = request.DepartDate,
                ReturnDate = request.ReturnDate,
                Tags = request.Tags ?? new List<string>(),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow
            };

            db.Trips.Add(trip);
            await db.SaveChangesAsync();

            var response = new TripResponse(
                trip.Id,
                trip.UserId,
                trip.Destination,
                trip.DepartDate,
                trip.ReturnDate,
                trip.Tags,
                trip.IsArchived,
                trip.CreatedAt
            );
            return Results.Created($"/trips/{trip.Id}", response);
        })
        .WithName("CreateTrip")
        .WithSummary("Create a new trip")
        .WithDescription("Creates a new trip destination with departure and return dates for the authenticated user, validating that dates do not overlap with existing trips.")
        .Produces<TripResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateTripRequest request,
            IValidator<UpdateTripRequest> validator,
            ClaimsPrincipal userClaims,
            TripDbContext db
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var userId = userClaims.GetUserId();

            var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (trip == null)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            if (trip.IsArchived && request.IsArchived != false)
            {
                return Results.BadRequest(new ErrorResponse("Archived trips cannot be edited. Please unarchive the trip first."));
            }

            if (request.DepartDate != default || request.ReturnDate != default)
            {
                var targetDepart = request.DepartDate != default ? request.DepartDate : trip.DepartDate;
                var targetReturn = request.ReturnDate != default ? request.ReturnDate : trip.ReturnDate;

                var hasOverlap = await db.Trips.AnyAsync(t =>
                    t.UserId == userId &&
                    t.Id != id &&
                    (
                        (targetDepart < t.ReturnDate && targetReturn > t.DepartDate) ||
                        (targetDepart == targetReturn && t.DepartDate == t.ReturnDate && targetDepart == t.DepartDate)
                    ));

                if (hasOverlap)
                {
                    return Results.BadRequest(new ErrorResponse("Trip dates overlap with an existing trip. Multiple trips cannot cover the same date range."));
                }
            }

            if (request.Destination != null) trip.Destination = request.Destination;
            if (request.DepartDate != default) trip.DepartDate = request.DepartDate;
            if (request.ReturnDate != default) trip.ReturnDate = request.ReturnDate;
            if (request.Tags != null) trip.Tags = request.Tags;
            if (request.IsArchived.HasValue) trip.IsArchived = request.IsArchived.Value;
            await db.SaveChangesAsync();
            return Results.Ok(new TripResponse(
                trip.Id,
                trip.UserId,
                trip.Destination,
                trip.DepartDate,
                trip.ReturnDate,
                trip.Tags,
                trip.IsArchived,
                trip.CreatedAt
            ));
        })
        .WithName("UpdateTrip")
        .WithSummary("Partially update an existing trip")
        .WithDescription("Updates destination, dates, tags, or archived status for an existing trip belonging to the authenticated user, validating date ranges.")
        .Produces<TripResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/archive", async (
            Guid id,
            ClaimsPrincipal userClaims,
            TripDbContext db
        ) =>
        {
            var userId = userClaims.GetUserId();
            var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (trip == null)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            trip.IsArchived = true;
            await db.SaveChangesAsync();

            return Results.Ok(new TripResponse(
                trip.Id,
                trip.UserId,
                trip.Destination,
                trip.DepartDate,
                trip.ReturnDate,
                trip.Tags,
                trip.IsArchived,
                trip.CreatedAt
            ));
        })
        .WithName("ArchiveTrip")
        .WithSummary("Archive a trip")
        .WithDescription("Sets isArchived to true for a specific trip belonging to the authenticated user.")
        .Produces<TripResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/unarchive", async (
            Guid id,
            ClaimsPrincipal userClaims,
            TripDbContext db
        ) =>
        {
            var userId = userClaims.GetUserId();
            var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (trip == null)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            trip.IsArchived = false;
            await db.SaveChangesAsync();

            return Results.Ok(new TripResponse(
                trip.Id,
                trip.UserId,
                trip.Destination,
                trip.DepartDate,
                trip.ReturnDate,
                trip.Tags,
                trip.IsArchived,
                trip.CreatedAt
            ));
        })
        .WithName("UnarchiveTrip")
        .WithSummary("Unarchive a trip")
        .WithDescription("Sets isArchived to false for a specific trip belonging to the authenticated user.")
        .Produces<TripResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal userClaims,
            TripDbContext db
        ) =>
        {
            var userId = userClaims.GetUserId();
            var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (trip == null)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            db.Trips.Remove(trip);
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteTrip")
        .WithSummary("Delete a trip by ID")
        .WithDescription("Deletes a trip belonging to the authenticated user.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/ai-recommendations", async (
            Guid id,
            ClaimsPrincipal userClaims,
            TripDbContext db,
            IHttpClientFactory httpFactory,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();

            var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (trip == null)
            {
                return Results.NotFound(new ErrorResponse("Trip not found"));
            }

            var tripDays = Math.Max(1, trip.ReturnDate.DayNumber - trip.DepartDate.DayNumber + 1);

            var weatherClient = httpFactory.CreateClient("WeatherService");
            WeatherDto? weather = null;
            try
            {
                var weatherUrl = $"/weather?city={Uri.EscapeDataString(trip.Destination.City)}&country={Uri.EscapeDataString(trip.Destination.Country)}&departDate={trip.DepartDate:yyyy-MM-dd}";
                weather = await weatherClient.GetFromJsonAsync<WeatherDto>(weatherUrl, ct);
            }
            catch
            {
            }
            var itemsClient = httpFactory.CreateClient("TripItemsService");
            var existingItemLabels = new List<string>();
            try
            {
                var itemsResponse = await itemsClient.GetFromJsonAsync<List<TripSectionGroupDto>>($"/trip-items/{trip.Id}", ct);
                if (itemsResponse != null)
                {
                    existingItemLabels = itemsResponse.SelectMany(s => s.Items).Select(i => i.Label).ToList();
                }
            }
            catch
            {
            }
            var aiClient = httpFactory.CreateClient("AIService");
            var aiRequest = new AiRecommendationRequestDto(
                City: trip.Destination.City,
                Country: trip.Destination.Country,
                TripDays: tripDays,
                TripType: trip.Tags != null && trip.Tags.Count > 0 ? string.Join(", ", trip.Tags) : "general",
                WeatherSummary: weather?.ConditionDescription ?? weather?.WeatherToday ?? "Mild",
                CurrentSeason: weather?.CurrentSeason ?? trip.Destination.CurrentSeason ?? "Summer",
                ExistingItems: existingItemLabels
            );
            var aiResponse = await aiClient.PostAsJsonAsync("/ai/recommendations", aiRequest, ct);
            if (!aiResponse.IsSuccessStatusCode)
            {
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, detail: "AI Service is temporarily unavailable");
            }
            var recommendations = await aiResponse.Content.ReadFromJsonAsync<AiRecommendationResponseDto>(cancellationToken: ct);
            return Results.Ok(recommendations);
        })
        .WithName("GetTripAiRecommendations")
        .WithSummary("Generate AI packing recommendations for trip")
        .WithDescription("Fetches trip weather, aggregates existing items, and requests AI packing recommendations tailored to destination and trip duration.")
        .Produces<AiRecommendationResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }
}