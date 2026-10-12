using FluentValidation;
using AIService.DTOs;

namespace AIService.Validators;

public class GenerateRecommendationsRequestValidator : AbstractValidator<GenerateRecommendationsRequest>
{
    public GenerateRecommendationsRequestValidator()
    {
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required");

        RuleFor(x => x.TripDays)
            .GreaterThan(0).WithMessage("Trip duration must be at least 1 day");
    }
}

public class TripGeneratorRequestValidator : AbstractValidator<TripGeneratorRequest>
{
    public TripGeneratorRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MinimumLength(3).WithMessage("Message is too short");
    }
}

public class AIPackingCopilotRequestValidator : AbstractValidator<AIPackingCopilotRequest>
{
    public AIPackingCopilotRequestValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty().WithMessage("TripId is required");

        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required")
            .MinimumLength(3).WithMessage("Prompt is too short");
    }
}