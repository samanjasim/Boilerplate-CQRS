namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class MrrCalculator
{
    public static double ReciprocalRank(IReadOnlyList<Guid> retrieved, ISet<Guid> relevant)
    {
        for (var i = 0; i < retrieved.Count; i++)
            if (relevant.Contains(retrieved[i])) return 1.0 / (i + 1);
        return 0.0;
    }

    public static double Mean(IReadOnlyList<double> reciprocalRanks)
    {
        if (reciprocalRanks.Count == 0) return 0.0;
        var sum = 0.0;
        for (var i = 0; i < reciprocalRanks.Count; i++) sum += reciprocalRanks[i];
        return sum / reciprocalRanks.Count;
    }
}
