using Starter.Module.AI.Application.Eval.Contracts;

namespace Starter.Module.AI.Application.Eval;

public interface IRagEvalHarness
{
    Task<EvalReport> RunAsync(
        EvalDataset dataset,
        EvalRunOptions options,
        CancellationToken ct);

    /// <summary>
    /// Same semantics as <see cref="RunAsync"/> but yields per-question progress
    /// events as each question finishes retrieval + judging. The final
    /// <see cref="RunCompletedEvent"/> carries the full <see cref="EvalReport"/>.
    /// Operators use this path for SSE streaming when datasets are large enough
    /// (&gt;20 questions) that a single-shot response would time out.
    /// </summary>
    IAsyncEnumerable<EvalStreamEvent> RunStreamingAsync(
        EvalDataset dataset,
        EvalRunOptions options,
        CancellationToken ct);
}
