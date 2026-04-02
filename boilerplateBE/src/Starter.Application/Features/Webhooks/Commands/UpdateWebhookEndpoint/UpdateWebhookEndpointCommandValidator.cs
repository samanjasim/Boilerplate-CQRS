using FluentValidation;

namespace Starter.Application.Features.Webhooks.Commands.UpdateWebhookEndpoint;

public sealed class UpdateWebhookEndpointCommandValidator : AbstractValidator<UpdateWebhookEndpointCommand>
{
    public UpdateWebhookEndpointCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Endpoint ID is required.");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Webhook URL is required.")
            .MaximumLength(2000).WithMessage("Webhook URL must not exceed 2000 characters.");

        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("At least one event type is required.");
    }
}
