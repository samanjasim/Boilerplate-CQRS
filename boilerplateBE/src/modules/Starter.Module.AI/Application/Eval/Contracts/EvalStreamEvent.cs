using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Module.AI.Application.Eval.Contracts;

/// <summary>
/// Discriminated union of events emitted by <see cref="IRagEvalHarness.RunStreamingAsync"/>.
/// Serialised as Server-Sent Events by <c>AiEvalController.StreamFaithfulness</c>.
/// </summary>
public abstract record EvalStreamEvent(string Type);

public sealed record RunStartedEvent(
    string DatasetName,
    string Language,
    int QuestionCount) : EvalStreamEvent("run_started");

public sealed record QuestionCompletedEvent(
    string QuestionId,
    string Query,
    int Index,
    int Total,
    double RecallAt5,
    double ReciprocalRank,
    double TotalLatencyMs,
    FaithfulnessQuestionResult? Faithfulness) : EvalStreamEvent("question_completed");

public sealed record RunCompletedEvent(EvalReport Report) : EvalStreamEvent("run_completed");

public sealed record RunErrorEvent(string Message) : EvalStreamEvent("run_error");
