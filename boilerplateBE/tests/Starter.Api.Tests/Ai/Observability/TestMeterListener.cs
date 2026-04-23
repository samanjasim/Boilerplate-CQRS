using System.Diagnostics.Metrics;

namespace Starter.Api.Tests.Ai.Observability;

/// <summary>
/// In-memory MeterListener that captures every instrument recording for a specific meter.
/// Use <see cref="Snapshot"/> from a test to assert on emitted measurements.
/// </summary>
public sealed class TestMeterListener : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<Measurement> _measurements = new();
    private readonly object _lock = new();

    public TestMeterListener(string meterName)
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName)
                l.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>(Record);
        _listener.SetMeasurementEventCallback<double>(Record);
        _listener.Start();
    }

    private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) where T : struct
    {
        var tagDict = new Dictionary<string, object?>(tags.Length);
        foreach (var kv in tags) tagDict[kv.Key] = kv.Value;
        lock (_lock) _measurements.Add(new Measurement(instrument.Name, Convert.ToDouble(value), tagDict));
    }

    public IReadOnlyList<Measurement> Snapshot()
    {
        lock (_lock) return _measurements.ToArray();
    }

    public void Dispose() => _listener.Dispose();

    public sealed record Measurement(string InstrumentName, double Value, IReadOnlyDictionary<string, object?> Tags);
}
