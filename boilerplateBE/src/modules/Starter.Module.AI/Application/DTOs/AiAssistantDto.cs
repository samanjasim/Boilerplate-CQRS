using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiAssistantDto(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    IReadOnlyList<string> EnabledToolNames,
    IReadOnlyList<Guid> KnowledgeBaseDocIds,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
