namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalDataset(
    string Name,
    string Language,
    string? Description,
    IReadOnlyList<EvalDocument> Documents,
    IReadOnlyList<EvalQuestion> Questions);
