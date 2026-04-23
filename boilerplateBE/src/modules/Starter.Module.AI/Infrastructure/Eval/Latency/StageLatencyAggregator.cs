using System.Diagnostics.Metrics;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Infrastructure.Observability;

namespace Starter.Module.AI.Infrastructure.Eval.Latency;

public static class StageLatencyAggregator
{
    public static LatencyCapture BeginCapture() => new();

    public static LatencyMetrics Aggregate(IReadOnlyDictionary<string, List<double>> perStage)
    {
        var result = new Dictionary<string, StagePercentiles>(perStage.Count);
        foreach (var (stage, values) in perStage)
        {
            if (values.Count == 0) continue;
            var sorted = values.ToArray();
            Array.Sort(sorted);
            result[stage] = new StagePercentiles(
                P50: Percentile(sorted, 0.50),
                P95: Percentile(sorted, 0.95),
                P99: Percentile(sorted, 0.99));
        }
        return new LatencyMetrics(result);
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 1) return sorted[0];
        var rank = (int)Math.Ceiling(p * sorted.Length) - 1;
        if (rank < 0) rank = 0;
        if (rank >= sorted.Length) rank = sorted.Length - 1;
        return sorted[rank];
    }

    public sealed class LatencyCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly Dictionary<string, List<double>> _buckets = new();
        private readonly object _lock = new();
        private bool _disposed;

        internal LatencyCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AiRagMetrics.MeterName
                        && instrument.Name == "rag.stage.duration")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<double>(OnMeasurement);
            _listener.Start();
        }

        private void OnMeasurement(
            Instrument instrument,
            double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            string? stage = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "rag.stage") { stage = tag.Value?.ToString(); break; }
            }
            if (stage is null) return;

            lock (_lock)
            {
                if (!_buckets.TryGetValue(stage, out var list))
                {
                    list = new List<double>();
                    _buckets[stage] = list;
                }
                list.Add(measurement);
            }
        }

        public IReadOnlyDictionary<string, double[]> Stop()
        {
            lock (_lock)
            {
                _listener.Dispose();
                _disposed = true;
                return _buckets.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
            }
        }

        public void Dispose()
        {
            if (!_disposed) _listener.Dispose();
        }
    }
}
