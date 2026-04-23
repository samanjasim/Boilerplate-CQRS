using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Api.Tests.Ai.Fakes;

internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public int VectorSize => 1536;

    public Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null,
        AiRequestType requestType = AiRequestType.Embedding)
        => Task.FromResult(texts.Select(_ => new float[1536]).ToArray());
}
