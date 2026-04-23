using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Fakes;

internal sealed class FakeNeighborExpander : INeighborExpander
{
    public Guid CapturedTenantId { get; private set; }
    public int CapturedAnchorCount { get; private set; }
    public int CapturedWindowSize { get; private set; }
    public bool WasCalled { get; private set; }
    private readonly IReadOnlyList<RetrievedChunk> _returns;

    public FakeNeighborExpander(IReadOnlyList<RetrievedChunk> returns) => _returns = returns;

    public Task<IReadOnlyList<RetrievedChunk>> ExpandAsync(
        Guid tenantId,
        IReadOnlyList<RetrievedChunk> anchors,
        int windowSize,
        CancellationToken ct)
    {
        WasCalled = true;
        CapturedTenantId = tenantId;
        CapturedAnchorCount = anchors.Count;
        CapturedWindowSize = windowSize;
        return Task.FromResult(_returns);
    }
}

internal sealed class ThrowingNeighborExpander : INeighborExpander
{
    public bool WasCalled { get; private set; }

    public Task<IReadOnlyList<RetrievedChunk>> ExpandAsync(
        Guid tenantId,
        IReadOnlyList<RetrievedChunk> anchors,
        int windowSize,
        CancellationToken ct)
    {
        WasCalled = true;
        throw new InvalidOperationException("NeighborExpander should not have been called");
    }
}
