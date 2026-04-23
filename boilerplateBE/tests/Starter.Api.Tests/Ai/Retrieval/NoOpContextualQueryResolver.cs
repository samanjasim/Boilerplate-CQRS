using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// Always returns the raw latest message. Used by pre-4b-5 tests that should
/// exercise the rest of the retrieval pipeline without paying for the
/// contextualize stage.
/// </summary>
internal sealed class NoOpContextualQueryResolver : IContextualQueryResolver
{
    public Task<string> ResolveAsync(
        string latestUserMessage,
        IReadOnlyList<RagHistoryMessage> history,
        string? language,
        CancellationToken ct)
        => Task.FromResult(latestUserMessage);
}
