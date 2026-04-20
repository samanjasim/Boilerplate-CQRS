using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

public class AiRagMetricsTests
{
    [Fact]
    public void Meter_name_is_stable()
    {
        AiRagMetrics.MeterName.Should().Be("Starter.Module.AI.Rag");
    }

    [Fact]
    public void All_instruments_use_the_shared_meter()
    {
        AiRagMetrics.RetrievalRequests.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.StageDuration.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.StageOutcome.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.CacheRequests.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.FusionCandidates.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.ContextTokens.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.ContextTruncated.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.DegradedStages.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.RerankReordered.Meter.Name.Should().Be(AiRagMetrics.MeterName);
        AiRagMetrics.KeywordHits.Meter.Name.Should().Be(AiRagMetrics.MeterName);
    }

    [Fact]
    public void OpenTelemetry_meter_registration_includes_AiRag()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "true",
                ["OpenTelemetry:ServiceName"] = "test",
                ["OpenTelemetry:OtlpEndpoint"] = "http://127.0.0.1:4318",
            }).Build();

        // Attach an in-memory metric reader so we can verify the meter is subscribed by the OTel pipeline.
        var collected = new List<OpenTelemetry.Metrics.Metric>();
        services.AddOpenTelemetry().WithMetrics(m => m.AddReader(new OpenTelemetry.Metrics.BaseExportingMetricReader(new CollectingMetricExporter(collected))));

        Starter.Api.Configurations.OpenTelemetryConfiguration.AddOpenTelemetryObservability(services, config);
        var provider = services.BuildServiceProvider();

        var meterProvider = provider.GetRequiredService<OpenTelemetry.Metrics.MeterProvider>();
        meterProvider.Should().NotBeNull("OTel MeterProvider must be registered");

        // Emit a measurement, then force a collect so our custom exporter receives it.
        AiRagMetrics.RetrievalRequests.Add(1);
        meterProvider.ForceFlush();

        collected.Should().Contain(m => m.MeterName == AiRagMetrics.MeterName,
            "the Starter.Module.AI.Rag meter must be wired into the OTel metrics pipeline");
    }

    private sealed class CollectingMetricExporter : OpenTelemetry.BaseExporter<OpenTelemetry.Metrics.Metric>
    {
        private readonly List<OpenTelemetry.Metrics.Metric> _sink;
        public CollectingMetricExporter(List<OpenTelemetry.Metrics.Metric> sink) => _sink = sink;

        public override OpenTelemetry.ExportResult Export(in OpenTelemetry.Batch<OpenTelemetry.Metrics.Metric> batch)
        {
            foreach (var m in batch) _sink.Add(m);
            return OpenTelemetry.ExportResult.Success;
        }
    }
}
