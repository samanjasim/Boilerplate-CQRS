using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

internal interface IContentModerator
{
    /// <summary>
    /// Scans a piece of text for unsafe content per the resolved profile.
    /// Implementations must NOT throw on transient provider errors — return
    /// <see cref="ModerationVerdict.Unavailable"/>; the decorator decides
    /// FailOpen / FailClosed based on the profile.
    /// </summary>
    Task<ModerationVerdict> ScanAsync(
        string text,
        ModerationStage stage,
        ResolvedSafetyProfile profile,
        string? language,
        CancellationToken ct);
}
