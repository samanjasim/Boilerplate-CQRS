using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record PendingApprovalDto(
    Guid Id,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    string CommandTypeName,
    string ArgumentsJson,
    string? ReasonHint,
    PendingApprovalStatus Status,
    Guid? RequestingUserId,
    Guid? DecisionUserId,
    string? DecisionReason,
    DateTime? DecidedAt,
    DateTime ExpiresAt,
    DateTime CreatedAt);
