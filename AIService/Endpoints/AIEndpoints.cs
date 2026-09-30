using FluentValidation;
using AIService.DTOs;
using AIService.Services;

namespace AIService.Endpoints;

public static class AIEndpoints
{
    public static RouteGroupBuilder MapAIEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/recommendations", async (
            GenerateRecommendationsRequest request,
            IValidator<GenerateRecommendationsRequest> validator,
            IAIService aiService,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request);
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
        });


        return group;
    }
}