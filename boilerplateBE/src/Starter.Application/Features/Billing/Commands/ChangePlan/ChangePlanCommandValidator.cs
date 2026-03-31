using FluentValidation;

namespace Starter.Application.Features.Billing.Commands.ChangePlan;

public sealed class ChangePlanCommandValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
    }
}
