using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Events;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

internal sealed class CommunicationUserEventHandler(
    IUserReader userReader,
    ICurrentUserService currentUserService,
    ITriggerRuleEvaluator triggerRuleEvaluator,
    ILogger<CommunicationUserEventHandler> logger)
    : INotificationHandler<UserCreatedEvent>,
      INotificationHandler<UserUpdatedEvent>
{
    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Resolve tenant: prefer the event's TenantId (always available since the entity
        // carries it), then fall back to acting user's tenant or user reader.
        var tenantId = notification.TenantId ?? currentUserService.TenantId;

        if (!tenantId.HasValue)
        {
            // Fallback to reading the created user (works for post-commit scenarios)
            var user = await userReader.GetAsync(notification.UserId, cancellationToken);
            tenantId = user?.TenantId;
        }

        if (!tenantId.HasValue) return;

        logger.LogInformation(
            "Communication handling UserCreatedEvent for {UserId} in tenant {TenantId}",
            notification.UserId, tenantId.Value);

        await triggerRuleEvaluator.EvaluateAsync("user.created", tenantId.Value, notification.UserId,
            new Dictionary<string, object>
            {
                ["userId"] = notification.UserId.ToString(),
                ["email"] = notification.Email,
                ["userName"] = notification.FullName,
                ["fullName"] = notification.FullName,
                ["appName"] = "Application"
            }, cancellationToken);
    }

    public async Task Handle(UserUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var user = await userReader.GetAsync(notification.UserId, cancellationToken);
        var tenantId = currentUserService.TenantId ?? user?.TenantId;
        if (!tenantId.HasValue) return;

        await triggerRuleEvaluator.EvaluateAsync("user.updated", tenantId.Value, notification.UserId,
            new Dictionary<string, object>
            {
                ["userId"] = notification.UserId.ToString(),
                ["userName"] = user?.DisplayName ?? "",
                ["email"] = user?.Email ?? ""
            }, cancellationToken);
    }
}
