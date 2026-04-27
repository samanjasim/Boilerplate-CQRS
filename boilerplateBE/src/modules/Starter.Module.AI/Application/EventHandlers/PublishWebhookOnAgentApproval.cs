using System.Collections.Generic;
using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Ai.Events;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Infrastructure.Observability;

namespace Starter.Module.AI.Application.EventHandlers;

/// <summary>
/// Plan 5d-2 Task F3 — fan-out handler that emits the four
/// <c>ai.agent.approval.{pending,approved,denied,expired}</c> webhook events
/// whenever the matching domain event is raised by the AI module.
///
/// Webhook publication is best-effort: a failure to dispatch a webhook must never
/// roll back the originating command (approve/deny/expire decisions are already
/// committed by the time MediatR delivers the notification). All exceptions from
/// the publisher are caught and logged at warning level.
///
/// The companion <c>ai.moderation.blocked</c> webhook is not published here — it
/// is fired inline by <c>ContentModerationEnforcingAgentRuntime.BuildBlockedResult</c>
/// (Task D2) because no domain event is raised for moderation blocks.
///
/// Each terminal lifecycle transition (approved/denied/expired) increments
/// <see cref="AiAgentMetrics.PendingApprovals"/> with an outcome label so the
/// counter exposes a complete lifecycle view alongside the "created" increment
/// emitted by <c>ChatExecutionService</c> (D5) and the expiration job (G1).
/// </summary>
internal sealed class PublishWebhookOnAgentApproval(
    IWebhookPublisher publisher,
    ILogger<PublishWebhookOnAgentApproval> logger)
    : INotificationHandler<AgentApprovalPendingEvent>,
      INotificationHandler<AgentApprovalApprovedEvent>,
      INotificationHandler<AgentApprovalDeniedEvent>,
      INotificationHandler<AgentApprovalExpiredEvent>
{
    public Task Handle(AgentApprovalPendingEvent ev, CancellationToken ct) =>
        SafePublish("ai.agent.approval.pending", ev.TenantId, new
        {
            tenantId = ev.TenantId,
            approvalId = ev.ApprovalId,
            assistantId = ev.AssistantId,
            assistantName = ev.AssistantName,
            toolName = ev.ToolName,
            reason = ev.Reason,
            requestingUserId = ev.RequestingUserId,
            conversationId = ev.ConversationId,
            agentTaskId = ev.AgentTaskId,
            expiresAt = ev.ExpiresAt
        }, ct);

    public Task Handle(AgentApprovalApprovedEvent ev, CancellationToken ct)
    {
        AiAgentMetrics.PendingApprovals.Add(1,
            new KeyValuePair<string, object?>("ai.approval.outcome", "approved"));

        return SafePublish("ai.agent.approval.approved", ev.TenantId, new
        {
            tenantId = ev.TenantId,
            approvalId = ev.ApprovalId,
            assistantId = ev.AssistantId,
            assistantName = ev.AssistantName,
            toolName = ev.ToolName,
            requestingUserId = ev.RequestingUserId,
            decisionUserId = ev.DecisionUserId,
            decisionReason = ev.DecisionReason,
            conversationId = ev.ConversationId
        }, ct);
    }

    public Task Handle(AgentApprovalDeniedEvent ev, CancellationToken ct)
    {
        AiAgentMetrics.PendingApprovals.Add(1,
            new KeyValuePair<string, object?>("ai.approval.outcome", "denied"));

        return SafePublish("ai.agent.approval.denied", ev.TenantId, new
        {
            tenantId = ev.TenantId,
            approvalId = ev.ApprovalId,
            assistantId = ev.AssistantId,
            assistantName = ev.AssistantName,
            toolName = ev.ToolName,
            requestingUserId = ev.RequestingUserId,
            decisionUserId = ev.DecisionUserId,
            decisionReason = ev.DecisionReason,
            conversationId = ev.ConversationId
        }, ct);
    }

    public Task Handle(AgentApprovalExpiredEvent ev, CancellationToken ct)
    {
        AiAgentMetrics.PendingApprovals.Add(1,
            new KeyValuePair<string, object?>("ai.approval.outcome", "expired"));

        return SafePublish("ai.agent.approval.expired", ev.TenantId, new
        {
            tenantId = ev.TenantId,
            approvalId = ev.ApprovalId,
            assistantId = ev.AssistantId,
            assistantName = ev.AssistantName,
            toolName = ev.ToolName,
            requestingUserId = ev.RequestingUserId,
            expiredAt = ev.ExpiredAt
        }, ct);
    }

    private async Task SafePublish(string eventType, Guid tenantId, object payload, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(eventType, tenantId, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to publish webhook {EventType} for tenant {TenantId}",
                eventType, tenantId);
        }
    }
}
