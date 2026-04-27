namespace Starter.Module.AI.Application.DTOs;

public sealed record AiChatReplyDto(
    Guid ConversationId,
    AiMessageDto UserMessage,
    AiMessageDto AssistantMessage,
    string? PersonaSlug = null)
{
    /// <summary>
    /// Outcome classifier for the turn — "completed" (default happy path),
    /// "awaiting_approval" (a [DangerousAction] tool created a pending approval),
    /// or "blocked" (input or output was refused by content moderation).
    /// Null on the legacy success shape so existing clients keep working.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>The pending approval ID when <see cref="Status"/> is "awaiting_approval".</summary>
    public Guid? ApprovalId { get; init; }

    /// <summary>Approval expiry (UTC) when <see cref="Status"/> is "awaiting_approval".</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>The dangerous tool that triggered the approval pause.</summary>
    public string? ToolName { get; init; }

    /// <summary>Optional human-readable reason hint surfaced from the dispatcher.</summary>
    public string? ApprovalReason { get; init; }
}
