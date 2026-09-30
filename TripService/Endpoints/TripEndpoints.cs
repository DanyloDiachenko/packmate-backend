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
            ClaimsPrincipal userClaims,
            TripDbContext db
        ) =>
        {
            var userId = userClaims.GetUserId();
            var trips = await db.Trips
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.DepartDate)
                .Select(t => new TripResponse(
                    t.Id,
                    t.UserId,
                    t.Destination,
                    t.DepartDate,
                    t.ReturnDate,
                    t.TripType,
                    t.CreatedAt
                ))
                .ToListAsync();

            return Results.Ok(trips);
        })
        .WithName("GetUserTrips")
        .WithSummary("List all trips for the authenticated user")
        .WithDescription("Returns a list of all trips created by the authenticated user, ordered by departure date.")
        .Produces<List<TripResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal userClaims,
            TripDbContext db
        ) =>
        {
            var userId = userClaims.GetUserId();
            var trip = await db.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (trip == null)
            {
                return Results.NotFound(new ErrorResponse("Trip was not found"));
            }

            return Results.Ok(new TripResponse(
                trip.Id,
                trip.UserId,
                trip.Destination,
                trip.DepartDate,
                trip.ReturnDate,
                trip.TripType,
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

            var trip = new Trip
            {
                UserId = userId,
                Destination = request.Destination,
                DepartDate = request.DepartDate,
                ReturnDate = request.ReturnDate,
                TripType = request.TripType,
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
                trip.TripType,
                trip.CreatedAt
            );
            return Results.Created($"/trips/{trip.Id}", response);
        })
        .WithName("CreateTrip")
        .WithSummary("Create a new trip")
        .WithDescription("Creates a new trip destination with departure and return dates for the authenticated user.")
        .Produces<TripResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
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

            if (request.Destination != null) trip.Destination = request.Destination;
            if (request.DepartDate != default) trip.DepartDate = request.DepartDate;
            if (request.ReturnDate != default) trip.ReturnDate = request.ReturnDate;
            if (!string.IsNullOrWhiteSpace(request.TripType)) trip.TripType = request.TripType;
            await db.SaveChangesAsync();
            return Results.Ok(new TripResponse(
                trip.Id,
                trip.UserId,
                trip.Destination,
                trip.DepartDate,
                trip.ReturnDate,
                trip.TripType,
                trip.CreatedAt
            ));
        })
        .WithName("UpdateTrip")
        .WithSummary("Partially update an existing trip")
        .WithDescription("Updates destination, dates, or trip type for an existing trip belonging to the authenticated user.")
        .Produces<TripResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
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
                TripType: trip.TripType,
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