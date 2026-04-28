namespace Starter.Module.AI.Application.Services.Moderation;

/// <summary>
/// Strips personally-identifiable information from free-form text when the
/// resolved safety profile asks for it. Implementations must never throw —
/// any internal failure should surface via <see cref="RedactionResult.Failed"/>
/// with the original text returned unmodified, so a redactor outage cannot
/// take down the chat pipeline.
/// </summary>
internal interface IPiiRedactor
{
    Task<RedactionResult> RedactAsync(string text, ResolvedSafetyProfile profile, CancellationToken ct);
}
