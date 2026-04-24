using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// Returns an empty array so RagRetrievalService falls back to the original query
/// (exercising the same single-variant path real rewriter failures produce, without
/// depending on the rewriter implementation in tests).
/// </summary>
internal sealed class NoOpQueryRewriter : IQueryRewriter
{
    public Task<IReadOnlyList<string>> RewriteAsync(Guid tenantId, string query, string? language, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
