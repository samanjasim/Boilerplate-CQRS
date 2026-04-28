using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Ai.Events;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

/// <summary>
/// Subscribes to the four <c>AgentApproval*Event</c> records published by the AI module
/// (Plan 5d-2) and routes them through the existing <see cref="ITriggerRuleEvaluator"/>
/// so tenant-configured trigger rules can dispatch in-app / email / Slack notifications
/// for the approval lifecycle.
///
/// Lives in Communication because event-routing is a Communication concern; the AI
/// module's project never references Communication. The contract crosses the
/// boundary via <c>Starter.Abstractions.Ai.Events</c> (B3) which both modules already
/// depend on transitively through <c>Starter.Abstractions</c>.
///
/// Failures are logged at warning level and swallowed — a notification dispatch
/// problem must never cascade back into the AI runtime that raised the event.
/// </summary>
internal sealed class CommunicationAiEventHandler(
    ITriggerRuleEvaluator evaluator,
    IConfiguration configuration,
    ILogger<CommunicationAiEventHandler> logger)
    : INotificationHandler<AgentApprovalPendingEvent>,
      INotificationHandler<AgentApprovalApprovedEvent>,
      INotificationHandler<AgentApprovalDeniedEvent>,
      INotificationHandler<AgentApprovalExpiredEvent>
{
    public Task Handle(AgentApprovalPendingEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.pending", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["reason"] = ev.Reason ?? "",
            ["expiresAt"] = ev.ExpiresAt.ToString("o"),
            ["deepLink"] = BuildDeepLink(ev.ApprovalId)
        }, ct);

    public Task Handle(AgentApprovalApprovedEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.approved", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["decisionUserId"] = ev.DecisionUserId.ToString(),
            ["decisionReason"] = ev.DecisionReason ?? "",
            ["deepLink"] = BuildDeepLink(ev.ApprovalId)
        }, ct);

    public Task Handle(AgentApprovalDeniedEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.denied", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["decisionUserId"] = ev.DecisionUserId.ToString(),
            ["decisionReason"] = ev.DecisionReason,
            ["deepLink"] = BuildDeepLink(ev.ApprovalId)
        }, ct);

    public Task Handle(AgentApprovalExpiredEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.expired", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["expiredAt"] = ev.ExpiredAt.ToString("o"),
            ["deepLink"] = BuildDeepLink(ev.ApprovalId)
        }, ct);

    private async Task Evaluate(
        string eventName,
        Guid tenantId,
        Guid? actorUserId,
        Dictionary<string, object> data,
        CancellationToken ct)
    {
        try
        {
            await evaluator.EvaluateAsync(eventName, tenantId, actorUserId, data, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to evaluate trigger rules for {EventName} in tenant {TenantId}",
                eventName, tenantId);
        }
    }

    private string BuildDeepLink(Guid approvalId)
    {
        var basePath = configuration["FrontendUrl"]?.TrimEnd('/') ?? "";
        return $"{basePath}/ai/agents/approvals/{approvalId}";
    }
}
