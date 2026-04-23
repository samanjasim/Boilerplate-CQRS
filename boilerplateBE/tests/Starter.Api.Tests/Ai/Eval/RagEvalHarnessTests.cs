using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Infrastructure.Eval.Baseline;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;
using Xunit.Abstractions;

namespace Starter.Api.Tests.Ai.Eval;

[Collection(RagEvalCollectionDef.Name)]
public sealed class RagEvalHarnessTests(RagEvalFixture fixture, ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("AI_EVAL_ENABLED") == "1";
    private static bool UpdateBaseline =>
        Environment.GetEnvironmentVariable("UPDATE_EVAL_BASELINE") == "1";

    [Fact]
    public Task EvalHarness_EnglishDataset_PassesBaseline() =>
        RunBaselineCheckAsync("rag-eval-dataset-en.json", "eval-rerank-cache-en.json");

    [Fact]
    public Task EvalHarness_ArabicDataset_PassesBaseline() =>
        RunBaselineCheckAsync("rag-eval-dataset-ar.json", "eval-rerank-cache-ar.json");

    private async Task RunBaselineCheckAsync(string fixtureFile, string rerankCacheFile)
    {
        if (!Enabled)
        {
            output.WriteLine(
                "SKIPPED: AI_EVAL_ENABLED is not set to 1. " +
                "Set AI_EVAL_ENABLED=1 (and bring up Postgres + Qdrant + a live provider key) to run this test.");
            return;
        }

        var sp = fixture.BuildEvalServiceProvider();
        var harness = sp.GetRequiredService<IRagEvalHarness>();
        var settings = sp.GetRequiredService<IOptions<AiRagEvalSettings>>().Value;

        await fixture.SeedRerankCacheAsync(sp, rerankCacheFile);

        var fixturePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", fixtureFile);
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
            output.WriteLine($"Baseline updated for dataset '{report.DatasetName}'.");
            return;
        }

        var baseline = BaselineLoader.Load(baselinePath);
        baseline.IsSuccess.Should().BeTrue();

        if (!baseline.Value.Datasets.TryGetValue(report.DatasetName, out var datasetBaseline))
            throw new Xunit.Sdk.XunitException(
                $"Baseline snapshot has no entry for dataset '{report.DatasetName}'. " +
                $"Run with UPDATE_EVAL_BASELINE=1 to seed it, then commit {baselinePath}.");

        var comparison = BaselineComparator.Compare(
            datasetBaseline,
            ToSnapshot(report),
            settings.MetricTolerance,
            settings.LatencyTolerance);

        foreach (var warning in comparison.Warnings) output.WriteLine("WARN: " + warning);

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
