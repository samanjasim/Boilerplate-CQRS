using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

public sealed record HybridHit(Guid ChunkId, decimal SemanticScore, decimal KeywordScore, decimal HybridScore);

internal static class HybridScoreCalculator
{
    public static IReadOnlyList<HybridHit> Combine(
        IReadOnlyList<IReadOnlyList<VectorSearchHit>> semanticLists,
        IReadOnlyList<IReadOnlyList<KeywordSearchHit>> keywordLists,
        decimal vectorWeight,
        decimal keywordWeight,
        int rrfK,
        decimal minScore)
    {
        var scores = new Dictionary<Guid, decimal>();
        var maxSem = new Dictionary<Guid, decimal>();
        var maxKw = new Dictionary<Guid, decimal>();

        foreach (var list in semanticLists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var hit = list[rank];
                var contribution = vectorWeight / (rrfK + rank + 1);
                scores[hit.ChunkId] = scores.GetValueOrDefault(hit.ChunkId) + contribution;
                if (!maxSem.TryGetValue(hit.ChunkId, out var existing) || hit.Score > existing)
                    maxSem[hit.ChunkId] = hit.Score;
            }
        }

        foreach (var list in keywordLists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var hit = list[rank];
                var contribution = keywordWeight / (rrfK + rank + 1);
                scores[hit.ChunkId] = scores.GetValueOrDefault(hit.ChunkId) + contribution;
                if (!maxKw.TryGetValue(hit.ChunkId, out var existing) || hit.Score > existing)
                    maxKw[hit.ChunkId] = hit.Score;
            }
        }

        return scores
            .Where(kv => kv.Value >= minScore)
            .Select(kv => new HybridHit(
                kv.Key,
                maxSem.GetValueOrDefault(kv.Key),
                maxKw.GetValueOrDefault(kv.Key),
                kv.Value))
            .OrderByDescending(h => h.HybridScore)
            .ThenBy(h => h.ChunkId)
            .ToList();
    }
}
