using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class NeighborExpander : INeighborExpander
{
    private readonly AiDbContext _db;
    private readonly ILogger<NeighborExpander> _logger;

    public NeighborExpander(AiDbContext db, ILogger<NeighborExpander> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> ExpandAsync(
        Guid tenantId,
        IReadOnlyList<RetrievedChunk> anchors,
        int windowSize,
        CancellationToken ct)
    {
        if (anchors.Count == 0 || windowSize <= 0)
            return Array.Empty<RetrievedChunk>();

        var anchorPointIds = anchors.Select(a => a.ChunkId).ToHashSet();
        var anchorDocIds = anchors.Select(a => a.DocumentId).Distinct().ToList();

        // Tenant-scope + doc name lookup in one query.
        var docNameById = await _db.AiDocuments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && anchorDocIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        if (docNameById.Count == 0)
            return Array.Empty<RetrievedChunk>();

        // Merge overlapping windows per document so we issue one DB round-trip.
        var rangesByDoc = anchors
            .Where(a => docNameById.ContainsKey(a.DocumentId))
            .GroupBy(a => a.DocumentId)
            .ToDictionary(
                g => g.Key,
                g => MergeRanges(g.Select(a => (Math.Max(0, a.ChunkIndex - windowSize), a.ChunkIndex + windowSize))).ToList());

        // Single batched query — materialize chunks for in-scope docs, then apply
        // per-range predicate in memory (avoids EF InMemory OR-expression translation issues).
        var validDocIds = rangesByDoc.Keys.ToList();
        var all = await _db.AiDocumentChunks
            .AsNoTracking()
            .Where(c => c.ChunkLevel == "child" && validDocIds.Contains(c.DocumentId))
            .Select(c => new
            {
                c.Id,
                c.QdrantPointId,
                c.DocumentId,
                c.Content,
                c.ChunkIndex,
                c.PageNumber,
                c.SectionTitle,
                c.ChunkLevel,
                c.ParentChunkId
            })
            .ToListAsync(ct);

        var siblings = all
            .Where(c => !anchorPointIds.Contains(c.QdrantPointId))
            .Where(c => rangesByDoc[c.DocumentId].Any(r => c.ChunkIndex >= r.Start && c.ChunkIndex <= r.End))
            .OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex)
            .Select(c => new RetrievedChunk(
                ChunkId: c.QdrantPointId,
                DocumentId: c.DocumentId,
                DocumentName: docNameById.GetValueOrDefault(c.DocumentId, string.Empty),
                Content: c.Content,
                SectionTitle: c.SectionTitle,
                PageNumber: c.PageNumber,
                ChunkLevel: c.ChunkLevel,
                SemanticScore: 0m,
                KeywordScore: 0m,
                HybridScore: 0m,
                ParentChunkId: c.ParentChunkId,
                ChunkIndex: c.ChunkIndex))
            .ToList();

        _logger.LogDebug(
            "NeighborExpander: {AnchorCount} anchors → {SiblingCount} siblings (windowSize={WindowSize})",
            anchors.Count, siblings.Count, windowSize);

        return siblings;
    }

    private static IEnumerable<(int Start, int End)> MergeRanges(IEnumerable<(int, int)> input)
    {
        var sorted = input.OrderBy(r => r.Item1).ToList();
        var merged = new List<(int Start, int End)>();
        foreach (var r in sorted)
        {
            if (merged.Count > 0 && merged[^1].End >= r.Item1 - 1)
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, r.Item2));
            else
                merged.Add((r.Item1, r.Item2));
        }
        return merged;
    }
}
