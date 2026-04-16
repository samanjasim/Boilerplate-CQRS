using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(IOptions<AiQdrantSettings> options, ILogger<QdrantVectorStore> logger)
    {
        var s = options.Value;
        _client = new QdrantClient(s.Host, s.GrpcPort, https: s.UseTls, apiKey: s.ApiKey);
        _logger = logger;
    }

    private static string CollectionName(Guid tenantId) => $"tenant_{tenantId:N}";

    public async Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct)
    {
        var name = CollectionName(tenantId);
        var collections = await _client.ListCollectionsAsync(ct);
        if (collections.Any(c => c == name))
        {
            var info = await _client.GetCollectionInfoAsync(name, ct);
            var existingDim = (int)(info?.Config?.Params?.VectorsConfig?.Params?.Size ?? 0);
            if (existingDim > 0 && existingDim != vectorSize)
            {
                throw new InvalidOperationException(
                    $"Qdrant collection '{name}' has vector dimension {existingDim} but the embedding " +
                    $"provider produced vectors of dimension {vectorSize}. Drop the collection (and its " +
                    "documents/chunks) before switching embedding models.");
            }
            return;
        }

        await _client.CreateCollectionAsync(
            collectionName: name,
            vectorsConfig: new VectorParams
            {
                Size = (ulong)vectorSize,
                Distance = Distance.Cosine
            },
            cancellationToken: ct);

        _logger.LogInformation("Created Qdrant collection {Collection} (dim={Dim})", name, vectorSize);
    }

    public async Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct)
    {
        if (points.Count == 0) return;

        var name = CollectionName(tenantId);
        var qPoints = points.Select(p => new PointStruct
        {
            Id = new PointId { Uuid = p.Id.ToString() },
            Vectors = p.Vector,
            Payload =
            {
                ["document_id"]     = p.Payload.DocumentId.ToString(),
                ["document_name"]   = p.Payload.DocumentName,
                ["chunk_level"]     = p.Payload.ChunkLevel,
                ["chunk_index"]     = p.Payload.ChunkIndex,
                ["section_title"]   = p.Payload.SectionTitle ?? string.Empty,
                ["page_number"]     = p.Payload.PageNumber ?? 0,
                ["parent_chunk_id"] = p.Payload.ParentChunkId?.ToString() ?? string.Empty,
                ["tenant_id"]       = p.Payload.TenantId.ToString(),
            }
        }).ToList();

        await _client.UpsertAsync(name, qPoints, cancellationToken: ct);
    }

    public async Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
    {
        var name = CollectionName(tenantId);
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "document_id",
                        Match = new Match { Text = documentId.ToString() }
                    }
                }
            }
        };
        await _client.DeleteAsync(name, filter, cancellationToken: ct);
    }

    public async Task DropCollectionAsync(Guid tenantId, CancellationToken ct)
    {
        var name = CollectionName(tenantId);
        await _client.DeleteCollectionAsync(name, cancellationToken: ct);
    }
}
