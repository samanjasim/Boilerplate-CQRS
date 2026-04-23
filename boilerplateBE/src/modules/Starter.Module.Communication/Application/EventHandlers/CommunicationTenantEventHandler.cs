using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Domain.Tenants.Events;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

internal sealed class CommunicationTenantEventHandler(
    ITriggerRuleEvaluator triggerRuleEvaluator,
    ILogger<CommunicationTenantEventHandler> logger)
    : INotificationHandler<TenantCreatedEvent>
{
    public async Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Communication handling TenantCreatedEvent for {TenantId}", notification.TenantId);

        await triggerRuleEvaluator.EvaluateAsync("tenant.registered", notification.TenantId, null,
            new Dictionary<string, object>
            {
                ["tenantId"] = notification.TenantId.ToString()
            }, cancellationToken);
    }
}
