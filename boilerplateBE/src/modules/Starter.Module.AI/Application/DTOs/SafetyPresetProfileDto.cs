using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record SafetyPresetProfileDto(
    Guid Id,
    Guid? TenantId,
    SafetyPreset Preset,
    ModerationProvider Provider,
    string CategoryThresholdsJson,
    string BlockedCategoriesJson,
    ModerationFailureMode FailureMode,
    bool RedactPii,
    int Version,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
