namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class NdcgCalculator
{
    // `retrieved` must be deduplicated by the caller; see RecallAtKCalculator.
    public static double Compute(
        IReadOnlyList<Guid> retrieved,
        ISet<Guid> relevant,
        int k)
    {
        if (relevant.Count == 0) return 0.0;

        var cutoff = Math.Min(k, retrieved.Count);
        var dcg = 0.0;
        for (var i = 0; i < cutoff; i++)
        {
            var rel = relevant.Contains(retrieved[i]) ? 1.0 : 0.0;
            dcg += rel / Math.Log2(i + 2);
        }

        var idealCount = Math.Min(k, relevant.Count);
        var idcg = 0.0;
        for (var i = 0; i < idealCount; i++) idcg += 1.0 / Math.Log2(i + 2);

        return idcg == 0.0 ? 0.0 : dcg / idcg;
    }
}
