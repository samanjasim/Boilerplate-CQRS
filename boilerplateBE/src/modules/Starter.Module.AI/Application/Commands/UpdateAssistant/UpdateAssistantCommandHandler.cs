using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UpdateAssistant;

internal sealed class UpdateAssistantCommandHandler(AiDbContext context)
    : IRequestHandler<UpdateAssistantCommand, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        UpdateAssistantCommand request,
        CancellationToken cancellationToken)
    {
        var assistant = await context.AiAssistants
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (assistant is null)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNotFound);

        // Uniqueness scoped within what the tenant can see (global filter already applied).
        var normalized = request.Name.Trim();
        if (normalized != assistant.Name)
        {
            var nameTaken = await context.AiAssistants
                .AnyAsync(a => a.Id != assistant.Id && a.Name == normalized, cancellationToken);
            if (nameTaken)
                return Result.Failure<AiAssistantDto>(AiErrors.AssistantNameAlreadyExists);
        }

        assistant.Update(
            name: normalized,
            description: request.Description,
            systemPrompt: request.SystemPrompt,
            provider: request.Provider,
            model: request.Model,
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            executionMode: request.ExecutionMode,
            maxAgentSteps: request.MaxAgentSteps);

        assistant.SetEnabledTools(request.EnabledToolNames ?? Array.Empty<string>());
        assistant.SetKnowledgeBase(request.KnowledgeBaseDocIds ?? Array.Empty<Guid>());
        assistant.SetActive(request.IsActive);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(assistant.ToDto());
    }
}
