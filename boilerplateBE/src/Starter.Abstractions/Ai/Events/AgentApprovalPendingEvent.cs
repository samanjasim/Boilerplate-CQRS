using MediatR;

namespace Starter.Abstractions.Ai.Events;

/// <summary>
/// Raised when an AI agent invokes a sensitive tool that requires
/// human approval before the underlying command can execute.
///
/// Lives in <c>Starter.Abstractions.Ai.Events</c> (rather than the AI
/// module's domain layer) because it crosses the AI ↔ Communication
/// boundary — Communication subscribes to dispatch the approval prompt
/// to the requesting user. Putting the event in Abstractions keeps
/// Communication free of a project reference on <c>Starter.Module.AI</c>.
/// </summary>
public sealed record AgentApprovalPendingEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    string? Reason,
    Guid? RequestingUserId,
    Guid? ConversationId,
    Guid? AgentTaskId,
    DateTime ExpiresAt) : INotification;
