using FluentValidation;
using AIService.DTOs;
using AIService.Services;

namespace AIService.Endpoints;

public static class AIEndpoints
{
    public static RouteGroupBuilder MapAIEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("AI Recommendations");

        group.MapPost("/recommendations", async (
            GenerateRecommendationsRequest request,
            IValidator<GenerateRecommendationsRequest> validator,
            IAIService aiService,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            try
            {
                var result = await aiService.GenerateRecommendationsAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "AI Generation Error"
                );
            }
        })
        .WithName("GenerateRecommendations")
        .WithSummary("Generate packing recommendations using LLM")
        .WithDescription("Generates categorized packing recommendations and tips based on destination, trip duration, trip type, season, weather conditions, and existing items.")
        .Produces<AIRecommendationResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPost("/trip-generator", async (
            TripGeneratorRequest request,
            IValidator<TripGeneratorRequest> validator,
            IAIService aiService,
            IHttpClientFactory httpFactory,
            HttpContext httpContext,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var existingTrips = new List<ExistingTripDateDto>();
            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                try
                {
                    var tripClient = httpFactory.CreateClient("TripService");
                    tripClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                    var tripsResponse = await tripClient.GetFromJsonAsync<TripServicePaginatedResponse>("/trips?isArchived=false", ct);
                    if (tripsResponse?.Trips != null && tripsResponse.Trips.Count > 0)
                    {
                        existingTrips = tripsResponse.Trips.Select(t => new ExistingTripDateDto(
                            t.Id,
                            $"{t.Destination.City}, {t.Destination.Country}".Trim(',', ' '),
                            t.DepartDate,
                            t.ReturnDate
                        )).ToList();
                    }
                }
                catch
                {
                }
            }

            try
            {
                var result = await aiService.GenerateTripAsync(request, existingTrips, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "AI Generation Error"
                );
            }
        })
        .WithName("GenerateTripFromPrompt")
        .WithSummary("Generate or update trip skeleton and packing items from free-form prompt")
        .WithDescription("Analyzes user text, extracts or modifies destination, dates, trip types, and packing items. Accepts an optional existing skeleton to refine or update it iteratively.")
        .Produces<TripGeneratorResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status502BadGateway);

        var handlePackingCopilot = async (
            AIPackingCopilotRequest request,
            IValidator<AIPackingCopilotRequest> validator,
            IAIService aiService,
            IHttpClientFactory httpFactory,
            HttpContext httpContext,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            var effectiveItems = request.ExistingItems ?? new List<CopilotExistingItemDto>();
            var destination = request.Destination;
            var dateRange = request.DateRange;
            var nightsCount = request.NightsCount;

            if (effectiveItems.Count == 0 && !string.IsNullOrEmpty(authHeader))
            {
                try
                {
                    var itemClient = httpFactory.CreateClient("TripItemsService");
                    itemClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                    var itemGroups = await itemClient.GetFromJsonAsync<List<TripSectionGroupDto>>($"/trip-items/{request.TripId}", ct);
                    if (itemGroups != null)
                    {
                        effectiveItems = itemGroups
                            .SelectMany(g => g.Items)
                            .Select(i => new CopilotExistingItemDto(
                                Id: i.Id,
                                Title: i.Title,
                                Section: i.Section,
                                Quantity: i.Quantity,
                                IsPacked: i.IsTaken,
                                Tag: i.Tag
                            ))
                            .ToList();
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(destination) && !string.IsNullOrEmpty(authHeader))
            {
                try
                {
                    var tripClient = httpFactory.CreateClient("TripService");
                    tripClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                    var trip = await tripClient.GetFromJsonAsync<TripDetailResponseDto>($"/trips/{request.TripId}", ct);
                    if (trip != null)
                    {
                        destination = $"{trip.Destination?.City}, {trip.Destination?.Country}".Trim(',', ' ');
                        dateRange ??= $"{trip.DepartDate:MMM d} – {trip.ReturnDate:MMM d}";
                        nightsCount ??= (trip.ReturnDate.DayNumber - trip.DepartDate.DayNumber);
                    }
                }
                catch
                {
                }
            }

            try
            {
                var preparedRequest = request with
                {
                    Destination = destination,
                    DateRange = dateRange,
                    NightsCount = nightsCount,
                    ExistingItems = effectiveItems
                };

                var result = await aiService.GeneratePackingCopilotDiffAsync(preparedRequest, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "AI Generation Error"
                );
            }
        };

        group.MapPost("/packing-copilot", handlePackingCopilot)
            .WithName("PackingCopilot")
            .WithSummary("AI Packing Copilot (Suggestions & Diff advisor)")
            .WithDescription("Analyzes current packing list and returns a structured diff patch of additions, removals, and modifications without mutating the database.")
            .Produces<AIPackingCopilotResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }
}