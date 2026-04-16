namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record EmbedAttribution(Guid? TenantId, Guid UserId);

public interface IEmbeddingService
{
    Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null);

    int VectorSize { get; }
}
