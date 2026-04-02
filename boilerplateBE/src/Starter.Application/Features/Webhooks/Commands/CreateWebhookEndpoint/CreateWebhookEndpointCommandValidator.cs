using FluentValidation;

namespace Starter.Application.Features.Webhooks.Commands.CreateWebhookEndpoint;

public sealed class CreateWebhookEndpointCommandValidator : AbstractValidator<CreateWebhookEndpointCommand>
{
    public CreateWebhookEndpointCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Webhook URL is required.")
            .MaximumLength(2000).WithMessage("Webhook URL must not exceed 2000 characters.")
            .Must(url => url.Contains("https://")).WithMessage("Webhook URL must use HTTPS.");

        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("At least one event type is required.");
    }
}
