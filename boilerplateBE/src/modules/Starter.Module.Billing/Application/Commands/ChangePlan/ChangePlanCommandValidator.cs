using FluentValidation;

namespace Starter.Module.Billing.Application.Commands.ChangePlan;

public sealed class ChangePlanCommandValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
    }
}
