using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;

public sealed class UpsertSafetyPresetProfileCommandValidator : AbstractValidator<UpsertSafetyPresetProfileCommand>
{
    public UpsertSafetyPresetProfileCommandValidator()
    {
        RuleFor(x => x.CategoryThresholdsJson).NotEmpty();
        RuleFor(x => x.BlockedCategoriesJson).NotEmpty();
    }
}
