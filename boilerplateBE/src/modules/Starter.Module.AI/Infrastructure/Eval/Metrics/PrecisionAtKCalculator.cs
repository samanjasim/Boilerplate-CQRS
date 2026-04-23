namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class PrecisionAtKCalculator
{
    public static double Compute(
        IReadOnlyList<Guid> retrieved,
        ISet<Guid> relevant,
        int k)
    {
        if (k <= 0 || retrieved.Count == 0) return 0.0;
        var cutoff = Math.Min(k, retrieved.Count);
        var hits = 0;
        for (var i = 0; i < cutoff; i++)
            if (relevant.Contains(retrieved[i])) hits++;
        return (double)hits / k;
    }
}
