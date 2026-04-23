namespace Starter.Module.AI.Application.Eval.Faithfulness;

public sealed record ClaimVerdict(string Text, string Verdict);

public sealed record FaithfulnessQuestionResult(
    string QuestionId,
    double Score,
    IReadOnlyList<ClaimVerdict> Claims,
    bool JudgeParseFailed);
