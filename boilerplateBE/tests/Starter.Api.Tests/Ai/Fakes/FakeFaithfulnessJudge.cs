using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Api.Tests.Ai.Fakes;

public sealed class FakeFaithfulnessJudge : IFaithfulnessJudge
{
    public List<FaithfulnessQuestionResult> Responses { get; } = new();
    public int CallCount { get; private set; }

    public Task<FaithfulnessQuestionResult> JudgeAsync(
        EvalQuestion question, string context, string answer, string? modelOverride, CancellationToken ct)
    {
        CallCount++;
        var response = CallCount - 1 < Responses.Count
            ? Responses[CallCount - 1]
            : new FaithfulnessQuestionResult(
                question.Id,
                0.8,
                new[] { new ClaimVerdict("stub", "SUPPORTED") },
                JudgeParseFailed: false);
        return Task.FromResult(response);
    }
}
