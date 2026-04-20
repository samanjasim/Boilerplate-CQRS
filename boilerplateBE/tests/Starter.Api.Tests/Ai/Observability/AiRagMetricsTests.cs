using FluentAssertions;
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
}
