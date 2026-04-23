using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Infrastructure.Eval.Baseline;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

[Collection(RagEvalCollectionDef.Name)]
public sealed class RagEvalHarnessTests(RagEvalFixture fixture)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("AI_EVAL_ENABLED") == "1";
    private static bool UpdateBaseline =>
        Environment.GetEnvironmentVariable("UPDATE_EVAL_BASELINE") == "1";

    [Fact]
    public async Task EvalHarness_EnglishDataset_PassesBaseline()
    {
        if (!Enabled) return;

        var sp = fixture.BuildEvalServiceProvider();
        var harness = sp.GetRequiredService<IRagEvalHarness>();
        var settings = sp.GetRequiredService<IOptions<AiRagEvalSettings>>().Value;

        var fixturePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-dataset-en.json");
        var datasetResult = EvalFixtureLoader.LoadFromFile(fixturePath);
        datasetResult.IsSuccess.Should().BeTrue();

        var report = await harness.RunAsync(
            datasetResult.Value,
            new EvalRunOptions(KValues: settings.KValues, WarmupQueries: settings.WarmupQueries),
            CancellationToken.None);

        var baselinePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-baseline.json");

        if (UpdateBaseline)
        {
            BaselineWriter.Update(baselinePath, report.DatasetName, ToSnapshot(report));
            return;
        }

        var baseline = BaselineLoader.Load(baselinePath);
        baseline.IsSuccess.Should().BeTrue();
        var comparison = BaselineComparator.Compare(
            baseline.Value.Datasets[report.DatasetName],
            ToSnapshot(report),
            settings.MetricTolerance,
            settings.LatencyTolerance);

        if (comparison.Failed)
            throw new Xunit.Sdk.XunitException(
                "Eval baseline regression:\n" + string.Join("\n", comparison.Failures));
    }

    [Fact]
    public async Task EvalHarness_ArabicDataset_PassesBaseline()
    {
        if (!Enabled) return;

        var sp = fixture.BuildEvalServiceProvider();
        var harness = sp.GetRequiredService<IRagEvalHarness>();
        var settings = sp.GetRequiredService<IOptions<AiRagEvalSettings>>().Value;

        var fixturePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-dataset-ar.json");
        var datasetResult = EvalFixtureLoader.LoadFromFile(fixturePath);
        datasetResult.IsSuccess.Should().BeTrue();

        var report = await harness.RunAsync(
            datasetResult.Value,
            new EvalRunOptions(KValues: settings.KValues, WarmupQueries: settings.WarmupQueries),
            CancellationToken.None);

        var baselinePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-baseline.json");

        if (UpdateBaseline)
        {
            BaselineWriter.Update(baselinePath, report.DatasetName, ToSnapshot(report));
            return;
        }

        var baseline = BaselineLoader.Load(baselinePath);
        baseline.IsSuccess.Should().BeTrue();
        var comparison = BaselineComparator.Compare(
            baseline.Value.Datasets[report.DatasetName],
            ToSnapshot(report),
            settings.MetricTolerance,
            settings.LatencyTolerance);

        if (comparison.Failed)
            throw new Xunit.Sdk.XunitException(
                "Eval baseline regression:\n" + string.Join("\n", comparison.Failures));
    }

    private static BaselineDatasetSnapshot ToSnapshot(EvalReport r) => new(
        RecallAtK: r.Metrics.Aggregate.RecallAtK,
        PrecisionAtK: r.Metrics.Aggregate.PrecisionAtK,
        NdcgAtK: r.Metrics.Aggregate.NdcgAtK,
        HitRateAtK: r.Metrics.Aggregate.HitRateAtK,
        Mrr: r.Metrics.Aggregate.Mrr,
        StageP95Ms: r.Latency.PerStage.ToDictionary(kv => kv.Key, kv => kv.Value.P95),
        DegradedStageCount: r.AggregateDegradedStages.Count);
}
