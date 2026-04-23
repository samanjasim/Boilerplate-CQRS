namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record PerQuestionResult(
    string QuestionId,
    string Query,
    IReadOnlyList<Guid> RetrievedDocumentIds,
    IReadOnlyList<Guid> RelevantDocumentIds,
    double RecallAt5,
    double RecallAt10,
    double ReciprocalRank,
    double TotalLatencyMs,
    IReadOnlyList<string> DegradedStages);
