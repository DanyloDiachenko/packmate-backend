

using TripService.DTOs;
using FluentValidation;

namespace TripService.Validators;

public class CreateTripValidator : AbstractValidator<CreateTripRequest>
{
    public CreateTripValidator()
    {
        RuleFor(x => x.Destination).NotNull().WithMessage("Destination is required");
        RuleFor(x => x.Destination.Country).NotEmpty().WithMessage("Country is required");
        RuleFor(x => x.Destination.City).NotEmpty().WithMessage("City is required");

        RuleFor(x => x.DepartDate)
        .NotNull().WithMessage("Depart date is required");

        RuleFor(x => x.ReturnDate)
        .NotNull().WithMessage("Return date is required")
        .GreaterThanOrEqualTo(x => x.DepartDate)
        .WithMessage("Return date must be greater than or equal to depart date");

        RuleFor(x => x.Tags)
        .NotNull().WithMessage("Tags are required");

        RuleForEach(x => x.Tags)
        .MaximumLength(50).WithMessage("Each tag cannot exceed 50 characters");
    }
}

public class UpdateTripValidator : AbstractValidator<UpdateTripRequest>
{
    public UpdateTripValidator()
    {
        When(x => x.DepartDate != default && x.ReturnDate != default, () =>
        {
            RuleFor(x => x.ReturnDate)
                .GreaterThanOrEqualTo(x => x.DepartDate)
                .WithMessage("Return date cannot be earlier than departure date");
        });

        When(x => x.Tags != null, () =>
        {
            RuleForEach(x => x.Tags!)
                .MaximumLength(50).WithMessage("Each tag cannot exceed 50 characters");
        });
    }
}
