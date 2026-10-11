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

        return group;
    }
}