using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Diversification;

/// <summary>
/// Pure-function implementation of Maximal Marginal Relevance (Carbonell &amp; Goldstein, 1998).
/// Given a relevance-ranked pool of hybrid hits and their chunk embeddings, iteratively picks
/// the candidate that maximises <c>λ · rel(d) − (1−λ) · max sim(d, s)</c> where <c>s</c> ranges
/// over the already-selected set. <c>rel</c> is min-max normalised across the pool so λ is not
/// dominated by the absolute magnitude of rerank scores.
/// </summary>
internal static class MmrDiversifier
{
    public static IReadOnlyList<HybridHit> Diversify(
        IReadOnlyList<HybridHit> hits,
        IReadOnlyDictionary<Guid, float[]> embeddings,
        double lambda,
        int topK)
    {
        if (hits.Count == 0 || topK <= 0)
            return Array.Empty<HybridHit>();

        var clampedLambda = Math.Clamp(lambda, 0.0, 1.0);

        var usableHits = hits.Where(h => embeddings.ContainsKey(h.ChunkId)).ToList();
        if (usableHits.Count == 0)
            return Array.Empty<HybridHit>();

        if (clampedLambda >= 1.0 - 1e-9 || usableHits.Count <= topK)
            return usableHits.Take(topK).ToList();

        var rels = new Dictionary<Guid, double>(usableHits.Count);
        var scores = usableHits.Select(h => (double)h.HybridScore).ToList();
        var min = scores.Min();
        var max = scores.Max();
        var span = max - min;
        foreach (var h in usableHits)
        {
            rels[h.ChunkId] = span > 1e-12
                ? ((double)h.HybridScore - min) / span
                : 0.5;
        }

        var selected = new List<HybridHit>(topK);
        var remaining = new List<HybridHit>(usableHits);
        var selectedEmbeddings = new List<float[]>(topK);

        while (selected.Count < topK && remaining.Count > 0)
        {
            HybridHit? best = null;
            var bestScore = double.NegativeInfinity;

            foreach (var candidate in remaining)
            {
                var candidateVec = embeddings[candidate.ChunkId];
                var maxSim = 0.0;
                foreach (var sVec in selectedEmbeddings)
                {
                    var sim = CosineSimilarity(candidateVec, sVec);
                    if (sim > maxSim) maxSim = sim;
                }

                var mmrScore = clampedLambda * rels[candidate.ChunkId]
                               - (1.0 - clampedLambda) * maxSim;

                if (mmrScore > bestScore)
                {
                    bestScore = mmrScore;
                    best = candidate;
                }
            }

            if (best is null) break;
            selected.Add(best);
            selectedEmbeddings.Add(embeddings[best.ChunkId]);
            remaining.Remove(best);
        }

        return selected;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0.0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na < 1e-12 || nb < 1e-12) return 0.0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
