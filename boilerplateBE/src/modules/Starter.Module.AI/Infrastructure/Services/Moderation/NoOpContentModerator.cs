using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Registered when no moderation provider key is configured. Reports as unavailable;
/// the decorator's failure-mode logic determines whether FailOpen (Standard) or
/// FailClosed (ChildSafe / Pro) is enforced.
/// </summary>
internal sealed class NoOpContentModerator : IContentModerator
{
    public Task<ModerationVerdict> ScanAsync(
        string text, ModerationStage stage, ResolvedSafetyProfile profile,
        string? language, CancellationToken ct) =>
        Task.FromResult(ModerationVerdict.Unavailable(0));
}
