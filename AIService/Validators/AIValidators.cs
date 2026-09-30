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