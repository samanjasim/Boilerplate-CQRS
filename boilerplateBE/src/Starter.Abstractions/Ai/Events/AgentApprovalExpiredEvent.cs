using MediatR;

namespace Starter.Abstractions.Ai.Events;

/// <summary>
/// Raised when a pending agent action passes its <c>ExpiresAt</c>
/// deadline before any human decides it. The AI module discards the
/// queued command on receipt; Communication may notify the requesting
/// user that the request lapsed without a decision.
/// </summary>
public sealed record AgentApprovalExpiredEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    Guid? RequestingUserId,
    DateTime ExpiredAt) : INotification;
