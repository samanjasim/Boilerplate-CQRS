using MediatR;

namespace Starter.Abstractions.Ai.Events;

/// <summary>
/// Raised when a human approver denies the pending agent action.
/// The AI module discards the queued command on receipt;
/// Communication notifies the requesting user with the denial reason.
/// </summary>
public sealed record AgentApprovalDeniedEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    Guid? RequestingUserId,
    Guid DecisionUserId,
    string DecisionReason,
    Guid? ConversationId) : INotification;
