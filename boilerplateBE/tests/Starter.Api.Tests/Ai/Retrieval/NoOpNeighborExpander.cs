using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

internal sealed class NoOpNeighborExpander : INeighborExpander
{
    public Task<IReadOnlyList<RetrievedChunk>> ExpandAsync(Guid tenantId, IReadOnlyList<RetrievedChunk> anchors, int windowSize, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<RetrievedChunk>>(Array.Empty<RetrievedChunk>());
}
