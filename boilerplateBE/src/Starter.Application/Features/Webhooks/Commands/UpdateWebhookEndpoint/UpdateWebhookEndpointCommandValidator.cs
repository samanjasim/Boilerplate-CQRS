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
            .MaximumLength(2000).WithMessage("Webhook URL must not exceed 2000 characters.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            .WithMessage("Webhook URL must be a valid HTTPS URL.");

        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("At least one event type is required.");
    }
}
