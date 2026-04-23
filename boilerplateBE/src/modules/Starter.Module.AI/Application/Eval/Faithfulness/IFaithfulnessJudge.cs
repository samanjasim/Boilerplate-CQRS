using Starter.Module.AI.Application.Eval.Contracts;

namespace Starter.Module.AI.Application.Eval.Faithfulness;

public interface IFaithfulnessJudge
{
    Task<FaithfulnessQuestionResult> JudgeAsync(
        EvalQuestion question,
        string context,
        string answer,
        string? modelOverride,
        CancellationToken ct);
}
