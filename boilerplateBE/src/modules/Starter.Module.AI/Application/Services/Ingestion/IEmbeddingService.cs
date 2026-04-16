namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IEmbeddingService
{
    Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);

    int VectorSize { get; }
}
