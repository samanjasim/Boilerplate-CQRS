using MediatR;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Events;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

internal sealed class CommunicationSecurityEventHandler(
    IUserReader userReader,
    ICurrentUserService currentUserService,
    ITriggerRuleEvaluator triggerRuleEvaluator)
    : INotificationHandler<PasswordChangedEvent>
{
    public async Task Handle(PasswordChangedEvent notification, CancellationToken cancellationToken)
    {
        var user = await userReader.GetAsync(notification.UserId, cancellationToken);
        var tenantId = currentUserService.TenantId ?? user?.TenantId;
        if (!tenantId.HasValue) return;

        await triggerRuleEvaluator.EvaluateAsync("password.changed", tenantId.Value, notification.UserId,
            new Dictionary<string, object>
            {
                ["userId"] = notification.UserId.ToString(),
                ["userName"] = user?.DisplayName ?? "",
                ["email"] = user?.Email ?? ""
            }, cancellationToken);
    }
}
