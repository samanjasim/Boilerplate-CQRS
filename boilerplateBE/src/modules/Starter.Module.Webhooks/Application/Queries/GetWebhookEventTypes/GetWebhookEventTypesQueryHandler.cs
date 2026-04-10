using MediatR;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookEventTypes;

internal sealed class GetWebhookEventTypesQueryHandler
    : IRequestHandler<GetWebhookEventTypesQuery, Result<List<WebhookEventTypeDto>>>
{
    public Task<Result<List<WebhookEventTypeDto>>> Handle(
        GetWebhookEventTypesQuery request, CancellationToken cancellationToken)
    {
        var eventTypes = new List<WebhookEventTypeDto>
        {
            new("user.created", "Users", "Triggered when a new user is created"),
            new("user.updated", "Users", "Triggered when a user profile is updated"),
            new("file.uploaded", "Files", "Triggered when a file is uploaded"),
            new("file.deleted", "Files", "Triggered when a file is deleted"),
            new("role.created", "Roles", "Triggered when a role is created"),
            new("role.updated", "Roles", "Triggered when a role is updated"),
            new("invitation.accepted", "Users", "Triggered when an invitation is accepted"),
            new("subscription.changed", "Billing", "Triggered when a subscription plan changes"),
        };

        return Task.FromResult(Result.Success(eventTypes));
    }
}
