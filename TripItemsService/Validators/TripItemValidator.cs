

using FluentValidation;
using TripItemsService.DTOs;

namespace TripItemsService.Validators;

public class CreateTripItemsRequestValidator : AbstractValidator<CreateTripItemRequest>
{
    public CreateTripItemsRequestValidator()
    {
        RuleFor(x => x.Section)
            .NotEmpty().WithMessage("Section is required")
            .MaximumLength(50);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(255);

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

public class UpdateTripItemRequestValidator : AbstractValidator<UpdateTripItemRequest>
{
    public UpdateTripItemRequestValidator()
    {
        When(x => x.Quantity.HasValue, () =>
        {
            RuleFor(x => x.Quantity!.Value)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
        When(x => !string.IsNullOrEmpty(x.Title), () =>
        {
            RuleFor(x => x.Title!).MaximumLength(255);
        });
    }
}