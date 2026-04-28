using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record ModerationEventDto(
    Guid Id,
    Guid? TenantId,
    Guid? AssistantId,
    ModerationStage Stage,
    SafetyPreset Preset,
    ModerationOutcome Outcome,
    string CategoriesJson,
    ModerationProvider Provider,
    string? BlockedReason,
    int LatencyMs,
    DateTime CreatedAt);
