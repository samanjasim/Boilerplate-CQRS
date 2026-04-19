using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

public sealed record HybridHit(Guid ChunkId, decimal SemanticScore, decimal KeywordScore, decimal HybridScore);

internal static class HybridScoreCalculator
{
    public static IReadOnlyList<HybridHit> Combine(
        IReadOnlyList<VectorSearchHit> semantic,
        IReadOnlyList<KeywordSearchHit> keyword,
        decimal alpha,
        decimal minScore)
    {
        var semMap = semantic.ToDictionary(h => h.ChunkId, h => h.Score);
        var kwMap = keyword.ToDictionary(h => h.ChunkId, h => h.Score);

        var semNorm = Normalise(semMap);
        var kwNorm = Normalise(kwMap);

        var allIds = new HashSet<Guid>(semMap.Keys);
        foreach (var id in kwMap.Keys) allIds.Add(id);

        var merged = allIds
            .Select(id =>
            {
                var sNorm = semNorm.GetValueOrDefault(id, 0m);
                var kNorm = kwNorm.GetValueOrDefault(id, 0m);
                var hybrid = alpha * sNorm + (1m - alpha) * kNorm;
                var sRaw = semMap.GetValueOrDefault(id, 0m);
                var kRaw = kwMap.GetValueOrDefault(id, 0m);
                return new HybridHit(id, sRaw, kRaw, hybrid);
            })
            .Where(h => h.HybridScore >= minScore)
            .OrderByDescending(h => h.HybridScore)
            .ThenBy(h => h.ChunkId)
            .ToList();

        return merged;
    }

    private static Dictionary<Guid, decimal> Normalise(Dictionary<Guid, decimal> raw)
    {
        if (raw.Count == 0) return new();
        var min = raw.Values.Min();
        var max = raw.Values.Max();
        if (max == min) return raw.ToDictionary(kv => kv.Key, _ => 1.0m);
        var range = max - min;
        return raw.ToDictionary(kv => kv.Key, kv => (kv.Value - min) / range);
    }
}
