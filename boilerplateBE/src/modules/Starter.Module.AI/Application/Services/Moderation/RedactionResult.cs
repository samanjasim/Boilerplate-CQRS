using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

/// <summary>
/// Outcome of a single PII redaction pass. <see cref="Outcome"/> is
/// <see cref="ModerationOutcome.Redacted"/> when at least one pattern matched
/// and the text was rewritten, <see cref="ModerationOutcome.Allowed"/> when no
/// matches were found or redaction was disabled by the profile.
/// <see cref="Failed"/> is set when the redactor itself threw and we fell back
/// to returning the original text — callers can surface this for telemetry
/// without aborting the request.
/// </summary>
internal sealed record RedactionResult(
    ModerationOutcome Outcome,
    string Text,
    IReadOnlyDictionary<string, int> Hits,
    bool Failed = false);
