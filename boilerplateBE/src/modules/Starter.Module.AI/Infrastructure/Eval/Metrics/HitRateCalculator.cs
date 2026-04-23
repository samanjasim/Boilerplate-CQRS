namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class HitRateCalculator
{
    public static double Compute(
        IReadOnlyList<Guid> retrieved,
        ISet<Guid> relevant,
        int k)
    {
        var cutoff = Math.Min(k, retrieved.Count);
        for (var i = 0; i < cutoff; i++)
            if (relevant.Contains(retrieved[i])) return 1.0;
        return 0.0;
    }

    public static double Mean(IReadOnlyList<double> perQuestionHits)
    {
        if (perQuestionHits.Count == 0) return 0.0;
        var sum = 0.0;
        for (var i = 0; i < perQuestionHits.Count; i++) sum += perQuestionHits[i];
        return sum / perQuestionHits.Count;
    }
}
