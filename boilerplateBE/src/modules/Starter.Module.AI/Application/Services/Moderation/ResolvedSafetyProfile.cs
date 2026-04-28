using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

/// <summary>
/// The fully-resolved per-request safety profile that drives moderation decisions:
/// per-category thresholds, always-block category list, fail-open vs. fail-closed
/// behaviour when the provider is unreachable, and whether to redact PII.
/// </summary>
internal sealed record ResolvedSafetyProfile(
    SafetyPreset Preset,
    ModerationProvider Provider,
    IReadOnlyDictionary<string, double> CategoryThresholds,
    IReadOnlyList<string> BlockedCategories,
    ModerationFailureMode FailureMode,
    bool RedactPii);
