namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalDocument(
    Guid Id,
    string FileName,
    string Content,
    string Language);
