using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

/// <summary>
/// Outcome of a single moderation scan. <see cref="ProviderUnavailable"/> signals
/// "no decision was reached" — the decorator's failure-mode policy decides whether
/// to fail-open or fail-closed; never raise an exception from a moderator
/// implementation.
/// </summary>
internal sealed record ModerationVerdict(
    ModerationOutcome Outcome,
    IReadOnlyDictionary<string, double> Categories,
    string? BlockedReason,
    int LatencyMs,
    bool ProviderUnavailable = false)
{
    public static ModerationVerdict Allowed(int latencyMs) =>
        new(ModerationOutcome.Allowed, new Dictionary<string, double>(), null, latencyMs);

    public static ModerationVerdict Blocked(IReadOnlyDictionary<string, double> categories, string reason, int latencyMs) =>
        new(ModerationOutcome.Blocked, categories, reason, latencyMs);

    public static ModerationVerdict Unavailable(int latencyMs) =>
        new(ModerationOutcome.Allowed, new Dictionary<string, double>(), null, latencyMs, ProviderUnavailable: true);
}
