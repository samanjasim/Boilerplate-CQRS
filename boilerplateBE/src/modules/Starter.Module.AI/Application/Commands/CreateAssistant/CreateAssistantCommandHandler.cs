using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

internal sealed class CreateAssistantCommandHandler(
    AiDbContext context,
    ICurrentUserService currentUser,
    ISlugGenerator slugGenerator)
    : IRequestHandler<CreateAssistantCommand, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        CreateAssistantCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId;
        var normalized = request.Name.Trim();

        var nameTaken = await context.AiAssistants
            .IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId && a.Name == normalized, cancellationToken);
        if (nameTaken)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNameAlreadyExists);

        var existingSlugs = await context.AiAssistants
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Slug != "")
            .Select(a => a.Slug)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<string>(existingSlugs, StringComparer.Ordinal);

        string slug;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (taken.Contains(slug))
                return Result.Failure<AiAssistantDto>(AiErrors.AssistantSlugAlreadyExists(slug));
        }
        else
        {
            slug = slugGenerator.EnsureUnique(slugGenerator.Slugify(normalized), taken);
        }

        var assistant = AiAssistant.Create(
            tenantId: tenantId,
            name: normalized,
            description: request.Description,
            systemPrompt: request.SystemPrompt,
            createdByUserId: currentUser.UserId!.Value,
            provider: request.Provider,
            model: request.Model,
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            executionMode: request.ExecutionMode,
            maxAgentSteps: request.MaxAgentSteps,
            isActive: true,
            slug: slug);

        if (request.EnabledToolNames is { Count: > 0 })
            assistant.SetEnabledTools(request.EnabledToolNames);

        if (request.KnowledgeBaseDocIds is { Count: > 0 })
            assistant.SetKnowledgeBase(request.KnowledgeBaseDocIds);

        if (request.RagScope != Domain.Enums.AiRagScope.None)
            assistant.SetRagScope(request.RagScope);

        if (request.PersonaTargetSlugs is not null)
            assistant.SetPersonaTargets(request.PersonaTargetSlugs);

        context.AiAssistants.Add(assistant);

        // Plan 5d-1: pair with an AiAgentPrincipal so the agent can act as a security
        // subject (hybrid intersection in dispatcher). Same EF transaction.
        if (assistant.TenantId is { } principalTenantId)
        {
            var principal = AiAgentPrincipal.Create(assistant.Id, principalTenantId, assistant.IsActive);
            context.AiAgentPrincipals.Add(principal);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(assistant.ToDto());
    }
}
