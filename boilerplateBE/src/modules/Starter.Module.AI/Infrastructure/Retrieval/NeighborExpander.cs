using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class NeighborExpander : INeighborExpander
{
    private readonly AiDbContext _db;
    private readonly AiRagSettings _settings;
    private readonly ILogger<NeighborExpander> _logger;

    public NeighborExpander(
        AiDbContext db,
        IOptions<AiRagSettings> settings,
        ILogger<NeighborExpander> logger)
    {
        _db = db;
        _settings = settings.Value;
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

        // Keep the raw anchor ranges per document so we can attribute each sibling
        // back to the anchor that pulled it in (for score inheritance).
        var anchorsByDoc = anchors
            .Where(a => docNameById.ContainsKey(a.DocumentId))
            .GroupBy(a => a.DocumentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Merge overlapping windows per document so we issue one DB round-trip.
        var rangesByDoc = anchorsByDoc.ToDictionary(
            kv => kv.Key,
            kv => MergeRanges(kv.Value.Select(a => (Math.Max(0, a.ChunkIndex - windowSize), a.ChunkIndex + windowSize))).ToList());

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
                c.ParentChunkId,
                c.ChunkType
            })
            .ToListAsync(ct);

        var weight = _settings.NeighborScoreWeight;

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
                HybridScore: NearestAnchorScore(c.DocumentId, c.ChunkIndex, anchorsByDoc, windowSize) * weight,
                ParentChunkId: c.ParentChunkId,
                ChunkIndex: c.ChunkIndex,
                ChunkType: c.ChunkType))
            .ToList();

        _logger.LogDebug(
            "NeighborExpander: {AnchorCount} anchors → {SiblingCount} siblings (windowSize={WindowSize}, weight={Weight})",
            anchors.Count, siblings.Count, windowSize, weight);

        return siblings;
    }

    // Sibling inherits the HybridScore of whichever anchor pulled it in; if the
    // sibling sits inside multiple anchor windows (overlapping), we take the
    // strongest anchor.
    private static decimal NearestAnchorScore(
        Guid documentId,
        int chunkIndex,
        Dictionary<Guid, List<RetrievedChunk>> anchorsByDoc,
        int windowSize)
    {
        if (!anchorsByDoc.TryGetValue(documentId, out var docAnchors))
            return 0m;

        decimal best = 0m;
        foreach (var a in docAnchors)
        {
            if (Math.Abs(a.ChunkIndex - chunkIndex) <= windowSize && a.HybridScore > best)
                best = a.HybridScore;
        }
        return best;
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
