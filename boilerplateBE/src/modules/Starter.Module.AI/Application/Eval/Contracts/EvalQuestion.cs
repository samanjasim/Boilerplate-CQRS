namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalQuestion(
    string Id,
    string Query,
    IReadOnlyList<Guid> RelevantDocumentIds,
    IReadOnlyList<Guid>? RelevantChunkIds,
    string? ExpectedAnswerSnippet,
    IReadOnlyList<string> Tags);
