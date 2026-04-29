using MediatR;

namespace Starter.Abstractions.Ai.Events;

/// <summary>
/// Raised when a human approver grants the pending agent action.
/// The AI module proceeds with the queued command on receipt;
/// Communication notifies the requesting user that the action ran.
/// </summary>
public sealed record AgentApprovalApprovedEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    Guid? RequestingUserId,
    Guid DecisionUserId,
    string? DecisionReason,
    Guid? ConversationId) : INotification;
