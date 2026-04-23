namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalRunOptions(
    int[] KValues,
    bool IncludeFaithfulness = false,
    string? JudgeModelOverride = null,
    int WarmupQueries = 2,
    Guid? AssistantId = null,
    string? AssistantSystemPrompt = null,
    string? AssistantModel = null);
