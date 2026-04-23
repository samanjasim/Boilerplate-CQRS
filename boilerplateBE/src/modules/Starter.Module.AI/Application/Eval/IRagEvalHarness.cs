using Starter.Module.AI.Application.Eval.Contracts;

namespace Starter.Module.AI.Application.Eval;

public interface IRagEvalHarness
{
    Task<EvalReport> RunAsync(
        EvalDataset dataset,
        EvalRunOptions options,
        CancellationToken ct);
}
