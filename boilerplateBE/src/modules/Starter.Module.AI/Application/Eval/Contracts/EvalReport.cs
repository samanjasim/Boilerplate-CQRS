using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalReport(
    DateTime RunAt,
    string DatasetName,
    string Language,
    int QuestionCount,
    EvalMetrics Metrics,
    LatencyMetrics Latency,
    IReadOnlyList<PerQuestionResult> PerQuestion,
    IReadOnlyList<string> AggregateDegradedStages,
    FaithfulnessReport? Faithfulness);
