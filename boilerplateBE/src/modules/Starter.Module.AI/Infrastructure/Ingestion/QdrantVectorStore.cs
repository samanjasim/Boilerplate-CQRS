using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
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
                ["chunk_type"]      = (int)p.Payload.ChunkType,
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
                        Match = new Match { Keyword = documentId.ToString() }
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

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid tenantId,
        float[] queryVector,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
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
                        Key = "chunk_level",
                        Match = new Match { Keyword = "child" }
                    }
                }
            }
        };

        if (documentFilter is { Count: > 0 })
        {
            var keywords = new RepeatedStrings();
            foreach (var d in documentFilter)
                keywords.Strings.Add(d.ToString());

            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "document_id",
                    Match = new Match { Keywords = keywords }
                }
            });
        }

        var results = await _client.SearchAsync(
            collectionName: name,
            vector: queryVector,
            filter: filter,
            limit: (ulong)limit,
            cancellationToken: ct);

        return results
            .Select(hit => new VectorSearchHit(
                ChunkId: Guid.Parse(hit.Id.Uuid),
                Score: (decimal)hit.Score))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> pointIds,
        CancellationToken ct)
    {
        if (pointIds.Count == 0)
            return new Dictionary<Guid, float[]>();

        var name = CollectionName(tenantId);
        var ids = pointIds
            .Select(id => new PointId { Uuid = id.ToString() })
            .ToList();

        var points = await _client.RetrieveAsync(
            collectionName: name,
            ids: ids,
            withPayload: false,
            withVectors: true,
            cancellationToken: ct);

        var map = new Dictionary<Guid, float[]>(points.Count);
        foreach (var p in points)
        {
            if (p.Vectors?.Vector?.Data is not { Count: > 0 } data) continue;
            map[Guid.Parse(p.Id.Uuid)] = data.ToArray();
        }

        return map;
    }
}
