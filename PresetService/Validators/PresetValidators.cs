using FluentValidation;
using PresetService.DTOs;

namespace PresetService.Validators;

public class CreatePresetValidator : AbstractValidator<CreatePresetRequest>
{
    public CreatePresetValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Preset name is required")
            .MaximumLength(100).WithMessage("Preset name cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description cannot exceed 255 characters");

        RuleFor(x => x.Categories)
            .NotNull().WithMessage("Categories are required")
            .Must(c => c != null && c.Values.Sum(list => list?.Count ?? 0) > 0)
            .WithMessage("Preset cannot be empty. Please include at least one item.");

        When(x => x.Categories != null, () =>
        {
            RuleForEach(x => x.Categories!.Values)
                .ForEach(itemRule =>
                {
                    itemRule.SetValidator(new PresetItemDtoValidator());
                });
        });
    }
}

public class UpdatePresetValidator : AbstractValidator<UpdatePresetRequest>
{
    public UpdatePresetValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Preset name is required")
            .MaximumLength(100).WithMessage("Preset name cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description cannot exceed 255 characters");

        When(x => x.Categories != null, () =>
        {
            RuleForEach(x => x.Categories!.Values)
                .ForEach(itemRule =>
                {
                    itemRule.SetValidator(new PresetItemDtoValidator());
                });
        });
    }
}

public class PresetItemDtoValidator : AbstractValidator<PresetItemDto>
{
    public PresetItemDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Item title is required")
            .MaximumLength(255).WithMessage("Item title cannot exceed 255 characters");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0");

        RuleFor(x => x.Section)
            .NotEmpty().WithMessage("Section is required")
            .MaximumLength(100);
    }
}