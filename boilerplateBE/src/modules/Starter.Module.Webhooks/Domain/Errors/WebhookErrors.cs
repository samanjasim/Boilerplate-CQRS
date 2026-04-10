using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Domain.Errors;

public static class WebhookErrors
{
    public static readonly Error EndpointNotFound = Error.NotFound(
        "Webhook.EndpointNotFound",
        "The specified webhook endpoint was not found.");

    public static readonly Error EndpointNotActive = Error.Validation(
        "Webhook.EndpointNotActive",
        "The specified webhook endpoint is not active.");

    public static readonly Error InvalidUrl = Error.Validation(
        "Webhook.InvalidUrl",
        "The provided webhook URL is not valid.");

    public static readonly Error WebhooksDisabled = Error.Validation(
        "Webhook.WebhooksDisabled",
        "Webhooks are not enabled for your plan.");

    public static Error QuotaExceeded(int limit) =>
        Error.Validation("Webhook.QuotaExceeded",
            $"You have reached the maximum number of webhook endpoints allowed ({limit}).");
}
