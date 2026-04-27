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

        // Uniqueness scoped to the existing assistant's tenant — IgnoreQueryFilters makes
        // the scope explicit and avoids SuperAdmin crossing tenants on the global filter.
        var normalized = request.Name.Trim();
        if (normalized != assistant.Name)
        {
            var tenantId = assistant.TenantId;
            var nameTaken = await context.AiAssistants
                .IgnoreQueryFilters()
                .AnyAsync(
                    a => a.Id != assistant.Id
                         && a.TenantId == tenantId
                         && a.Name == normalized,
                    cancellationToken);
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
        assistant.SetRagScope(request.RagScope);
        assistant.SetActive(request.IsActive);

        if (request.Slug is not null)
        {
            var newSlug = request.Slug.Trim().ToLowerInvariant();
            if (newSlug != assistant.Slug)
            {
                var taken = await context.AiAssistants
                    .IgnoreQueryFilters()
                    .AnyAsync(a => a.TenantId == assistant.TenantId &&
                                   a.Slug == newSlug &&
                                   a.Id != assistant.Id,
                        cancellationToken);
                if (taken)
                    return Result.Failure<AiAssistantDto>(AiErrors.AssistantSlugAlreadyExists(newSlug));
                assistant.SetSlug(newSlug);
            }
        }

        if (request.PersonaTargetSlugs is not null)
            assistant.SetPersonaTargets(request.PersonaTargetSlugs);

        // Plan 5d-1: keep paired AiAgentPrincipal in lockstep with assistant.IsActive so
        // the principal cannot transact when the assistant is suspended. EF tracks the
        // principal already via AiAgentPrincipalId FK; locate by AiAssistantId and toggle.
        var principal = await context.AiAgentPrincipals
            .FirstOrDefaultAsync(p => p.AiAssistantId == assistant.Id, cancellationToken);
        if (principal is not null)
        {
            if (assistant.IsActive) principal.Activate();
            else principal.Deactivate();
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(assistant.ToDto());
    }
}
