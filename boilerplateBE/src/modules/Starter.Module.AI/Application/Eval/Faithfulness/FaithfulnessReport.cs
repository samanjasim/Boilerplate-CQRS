namespace Starter.Module.AI.Application.Eval.Faithfulness;

public sealed record FaithfulnessReport(
    double AggregateScore,
    int JudgeParseFailureCount,
    IReadOnlyList<FaithfulnessQuestionResult> PerQuestion);
