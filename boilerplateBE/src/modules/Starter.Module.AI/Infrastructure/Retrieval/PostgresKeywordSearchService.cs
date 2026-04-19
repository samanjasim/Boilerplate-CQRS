using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class PostgresKeywordSearchService : IKeywordSearchService
{
    private readonly AiDbContext _db;
    private readonly ILogger<PostgresKeywordSearchService> _logger;

    public PostgresKeywordSearchService(AiDbContext db, ILogger<PostgresKeywordSearchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return [];

        // ChunkId returned here is the qdrant_point_id so it joins the same ID space
        // the vector store emits — the hybrid merge is keyed on qdrant_point_id.
        // content_tsv is generated with 'simple' config so queries must use 'simple' too.
        var sql = @"
            SELECT c.qdrant_point_id AS ""ChunkId"",
                   ts_rank_cd(c.content_tsv, plainto_tsquery('simple', {0}))::numeric AS ""Score""
            FROM ai_document_chunks c
            INNER JOIN ai_documents d ON d.id = c.document_id
            WHERE (d.tenant_id = {1} OR (d.tenant_id IS NULL AND {1} = '00000000-0000-0000-0000-000000000000'::uuid))
              AND c.chunk_level = 'child'
              AND c.content_tsv @@ plainto_tsquery('simple', {0})
        ";

        var parameters = new List<object> { queryText, tenantId };
        if (documentFilter is { Count: > 0 })
        {
            sql += $" AND c.document_id = ANY({{{parameters.Count}}})";
            parameters.Add(documentFilter.ToArray());
        }

        sql += $" ORDER BY \"Score\" DESC LIMIT {{{parameters.Count}}}";
        parameters.Add(limit);

        var hits = await _db.Database
            .SqlQueryRaw<KeywordSearchHitRow>(sql, parameters.ToArray())
            .ToListAsync(ct);

        return hits.Select(h => new KeywordSearchHit(h.ChunkId, h.Score)).ToList();
    }

    private sealed record KeywordSearchHitRow(Guid ChunkId, decimal Score);
}
