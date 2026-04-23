using Microsoft.Extensions.Logging;
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
    IOptions<AiRagSettings> ragSettings,
    ILogger<RagEvalHarness> logger) : IRagEvalHarness
{
    public async Task<EvalReport> RunAsync(
        EvalDataset dataset,
        EvalRunOptions options,
        CancellationToken ct)
    {
        var syntheticTenantId = Guid.NewGuid();
        var uploaderId = currentUser.UserId ?? Guid.NewGuid();

        logger.LogInformation(
            "RAG eval run starting: dataset={Dataset} lang={Lang} questions={Questions} syntheticTenant={Tenant}",
            dataset.Name, dataset.Language, dataset.Questions.Count, syntheticTenantId);

        IReadOnlyDictionary<Guid, Guid> idMap;
        try
        {
            idMap = await ingester.IngestAsync(syntheticTenantId, uploaderId, dataset, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RAG eval ingest failed for dataset={Dataset}", dataset.Name);
            await TryDropCollectionAsync(syntheticTenantId);
            throw;
        }

        var reverseMap = idMap.ToDictionary(kv => kv.Value, kv => kv.Key);

        try
        {
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
                if (!perStageDurations.TryGetValue("total", out var totalBucket))
                    perStageDurations["total"] = totalBucket = [];
                totalBucket.Add(sw.Elapsed.TotalMilliseconds);
                foreach (var d in context.DegradedStages) aggregateDegraded.Add(d);

                // Dedupe at document granularity: multiple chunks from the same doc collapse to
                // one hit. Classical Recall@K / Precision@K / NDCG@K assume each id in the list
                // is distinct; without this step a doc with N chunks inflates metrics by N.
                var seen = new HashSet<Guid>();
                var retrievedFixtureIds = new List<Guid>();
                foreach (var child in context.Children)
                {
                    if (!reverseMap.TryGetValue(child.DocumentId, out var fid)) continue;
                    if (seen.Add(fid)) retrievedFixtureIds.Add(fid);
                }
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
                    // Answer generation uses the ASSISTANT'S system prompt + model so faithfulness
                    // scores reflect what a real user of that assistant would see. The judge model
                    // override is a separate concern and only applies to the judge call below.
                    var answerPrompt = BuildAnswerPrompt(options.AssistantSystemPrompt, ctx, question.Query);
                    AiCompletionResult? answer;
                    try
                    {
                        answer = await ai.CompleteAsync(
                            answerPrompt,
                            new AiCompletionOptions(Model: options.AssistantModel),
                            ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Faithfulness answer generation failed for question={QuestionId}", question.Id);
                        answer = null;
                    }
                    var judgement = await judge.JudgeAsync(
                        question, ctx, answer?.Content ?? "", options.JudgeModelOverride, ct);
                    faithfulnessResults.Add(judgement);
                }
            }

            var metrics = BuildMetrics(dataset, perQuestion, options.KValues);
            var latency = StageLatencyAggregator.Aggregate(perStageDurations);
            var faithfulness = faithfulnessResults is null ? null : BuildFaithfulness(faithfulnessResults);

            logger.LogInformation(
                "RAG eval run complete: dataset={Dataset} recall@5={R5:F3} mrr={Mrr:F3} degradedStages={Deg}",
                dataset.Name, metrics.Aggregate.RecallAtK.GetValueOrDefault(5),
                metrics.Aggregate.Mrr, aggregateDegraded.Count);

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
            await TryDropCollectionAsync(syntheticTenantId);
        }
    }

    private async Task TryDropCollectionAsync(Guid syntheticTenantId)
    {
        try
        {
            await vectors.DropCollectionAsync(syntheticTenantId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to drop Qdrant collection for syntheticTenant={Tenant} — manual cleanup may be needed",
                syntheticTenantId);
        }
    }

    private static string BuildAnswerPrompt(string? systemPrompt, string context, string query)
    {
        var header = string.IsNullOrWhiteSpace(systemPrompt)
            ? "Answer the question using only this context."
            : systemPrompt!.Trim();
        return $"{header}\n\nContext:\n{context}\n\nQuestion: {query}";
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
