using Microsoft.Extensions.Options;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Faithfulness;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Eval.Faithfulness;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Eval.Latency;
using Starter.Module.AI.Infrastructure.Eval.Metrics;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Eval;

public sealed class RagEvalHarness(
    EvalFixtureIngester ingester,
    IRagRetrievalService retrieval,
    IVectorStore vectors,
    IAiService ai,
    IFaithfulnessJudge judge,
    ICurrentUserService currentUser,
    IOptions<AiRagSettings> ragSettings) : IRagEvalHarness
{
    public async Task<EvalReport> RunAsync(
        EvalDataset dataset,
        EvalRunOptions options,
        CancellationToken ct)
    {
        // Synthetic tenantId per run so each eval run gets its own isolated Qdrant collection
        var syntheticTenantId = Guid.NewGuid();
        var uploaderId = currentUser.UserId ?? Guid.NewGuid();

        IReadOnlyDictionary<Guid, Guid> idMap;
        try
        {
            idMap = await ingester.IngestAsync(syntheticTenantId, uploaderId, dataset, ct);
        }
        catch
        {
            // Clean up even if ingest partially succeeded
            try { await vectors.DropCollectionAsync(syntheticTenantId, CancellationToken.None); } catch { }
            throw;
        }

        var reverseMap = idMap.ToDictionary(kv => kv.Value, kv => kv.Key);

        try
        {
            // Warmup queries (cache pre-warming)
            for (var i = 0; i < options.WarmupQueries && i < dataset.Questions.Count; i++)
                await retrieval.RetrieveForQueryAsync(
                    syntheticTenantId, dataset.Questions[i].Query, null,
                    ragSettings.Value.TopK, null, ragSettings.Value.IncludeParentContext, ct);

            var perQuestion = new List<PerQuestionResult>(dataset.Questions.Count);
            var perStageDurations = new Dictionary<string, List<double>>();
            var aggregateDegraded = new HashSet<string>();
            var faithfulnessResults = options.IncludeFaithfulness
                ? new List<FaithfulnessQuestionResult>(dataset.Questions.Count)
                : null;

            foreach (var question in dataset.Questions)
            {
                using var capture = StageLatencyAggregator.BeginCapture();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var context = await retrieval.RetrieveForQueryAsync(
                    syntheticTenantId, question.Query, null,
                    options.KValues.Max(), null, ragSettings.Value.IncludeParentContext, ct);
                sw.Stop();
                var durations = capture.Stop();

                foreach (var (stage, arr) in durations)
                {
                    if (!perStageDurations.TryGetValue(stage, out var bucket))
                        perStageDurations[stage] = bucket = [];
                    bucket.AddRange(arr);
                }
                if (!perStageDurations.ContainsKey("total"))
                    perStageDurations["total"] = [];
                perStageDurations["total"].Add(sw.Elapsed.TotalMilliseconds);
                foreach (var d in context.DegradedStages) aggregateDegraded.Add(d);

                var retrievedFixtureIds = context.Children
                    .Select(c => reverseMap.TryGetValue(c.DocumentId, out var fid) ? fid : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList();
                var relevantSet = new HashSet<Guid>(question.RelevantDocumentIds);

                perQuestion.Add(new PerQuestionResult(
                    QuestionId: question.Id,
                    Query: question.Query,
                    RetrievedDocumentIds: retrievedFixtureIds,
                    RelevantDocumentIds: question.RelevantDocumentIds,
                    RecallAt5: RecallAtKCalculator.Compute(retrievedFixtureIds, relevantSet, 5),
                    RecallAt10: RecallAtKCalculator.Compute(retrievedFixtureIds, relevantSet, 10),
                    ReciprocalRank: MrrCalculator.ReciprocalRank(retrievedFixtureIds, relevantSet),
                    TotalLatencyMs: sw.Elapsed.TotalMilliseconds,
                    DegradedStages: context.DegradedStages));

                if (faithfulnessResults is not null)
                {
                    var ctx = string.Join("\n---\n", context.Children.Select(c => c.Content));
                    var answer = await ai.CompleteAsync(
                        $"Answer the question using only this context.\n\nContext:\n{ctx}\n\nQuestion: {question.Query}",
                        new AiCompletionOptions(Model: options.JudgeModelOverride), ct);
                    var judgement = await judge.JudgeAsync(
                        question, ctx, answer?.Content ?? "", options.JudgeModelOverride, ct);
                    faithfulnessResults.Add(judgement);
                }
            }

            var metrics = BuildMetrics(dataset, perQuestion, options.KValues);
            var latency = StageLatencyAggregator.Aggregate(perStageDurations);
            var faithfulness = faithfulnessResults is null ? null : BuildFaithfulness(faithfulnessResults);

            return new EvalReport(
                RunAt: DateTime.UtcNow,
                DatasetName: dataset.Name,
                Language: dataset.Language,
                QuestionCount: dataset.Questions.Count,
                Metrics: metrics,
                Latency: latency,
                PerQuestion: perQuestion,
                AggregateDegradedStages: aggregateDegraded.ToList(),
                Faithfulness: faithfulness);
        }
        finally
        {
            try { await vectors.DropCollectionAsync(syntheticTenantId, CancellationToken.None); } catch { }
        }
    }

    private static EvalMetrics BuildMetrics(
        EvalDataset dataset,
        IReadOnlyList<PerQuestionResult> perQuestion,
        int[] kValues)
    {
        var questions = dataset.Questions;
        var bucket = ComputeBucket(questions, perQuestion, kValues);
        var byTag = questions
            .SelectMany(q => q.Tags.Select(t => (tag: t, q)))
            .GroupBy(x => x.tag)
            .ToDictionary(
                g => g.Key,
                g => ComputeBucket(
                    g.Select(x => x.q).ToList(),
                    perQuestion.Where(r => g.Any(x => x.q.Id == r.QuestionId)).ToList(),
                    kValues));
        return new EvalMetrics(
            Aggregate: bucket,
            PerLanguage: new Dictionary<string, MetricBucket> { [dataset.Language] = bucket },
            PerTag: byTag);
    }

    private static MetricBucket ComputeBucket(
        IReadOnlyList<EvalQuestion> questions,
        IReadOnlyList<PerQuestionResult> results,
        int[] kValues)
    {
        var resultById = results.ToDictionary(r => r.QuestionId);
        var recall = new Dictionary<int, double>();
        var precision = new Dictionary<int, double>();
        var ndcg = new Dictionary<int, double>();
        var hit = new Dictionary<int, double>();

        foreach (var k in kValues)
        {
            var recallVals = new List<double>();
            var precisionVals = new List<double>();
            var ndcgVals = new List<double>();
            var hitVals = new List<double>();

            foreach (var question in questions)
            {
                if (!resultById.TryGetValue(question.Id, out var result)) continue;
                var rel = new HashSet<Guid>(question.RelevantDocumentIds);
                recallVals.Add(RecallAtKCalculator.Compute(result.RetrievedDocumentIds, rel, k));
                precisionVals.Add(PrecisionAtKCalculator.Compute(result.RetrievedDocumentIds, rel, k));
                ndcgVals.Add(NdcgCalculator.Compute(result.RetrievedDocumentIds, rel, k));
                hitVals.Add(HitRateCalculator.Compute(result.RetrievedDocumentIds, rel, k));
            }

            recall[k] = recallVals.Count == 0 ? 0 : recallVals.Average();
            precision[k] = precisionVals.Count == 0 ? 0 : precisionVals.Average();
            ndcg[k] = ndcgVals.Count == 0 ? 0 : ndcgVals.Average();
            hit[k] = HitRateCalculator.Mean(hitVals);
        }

        var mrr = MrrCalculator.Mean(results.Select(r => r.ReciprocalRank).ToList());
        return new MetricBucket(recall, precision, ndcg, hit, mrr);
    }

    private static FaithfulnessReport BuildFaithfulness(IReadOnlyList<FaithfulnessQuestionResult> perQuestion)
    {
        var valid = perQuestion.Where(r => !r.JudgeParseFailed).ToList();
        var aggregate = valid.Count == 0 ? 0 : valid.Average(r => r.Score);
        var parseFailures = perQuestion.Count(r => r.JudgeParseFailed);
        return new FaithfulnessReport(aggregate, parseFailures, perQuestion);
    }
}
