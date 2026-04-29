using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Settings.ModelDefaults.UpsertModelDefault;

public sealed class UpsertModelDefaultCommandValidator : AbstractValidator<UpsertModelDefaultCommand>
{
    public UpsertModelDefaultCommandValidator()
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 128_000).When(x => x.MaxTokens.HasValue);
        RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0).When(x => x.Temperature.HasValue);
    }
}
