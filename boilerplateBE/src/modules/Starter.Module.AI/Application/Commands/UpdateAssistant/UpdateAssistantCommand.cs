using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UpdateAssistant;

public sealed record UpdateAssistantCommand(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    IReadOnlyList<string>? EnabledToolNames,
    IReadOnlyList<Guid>? KnowledgeBaseDocIds,
    bool IsActive,
    AiRagScope RagScope = AiRagScope.None)
    : IRequest<Result<AiAssistantDto>>, IAssistantInput;
