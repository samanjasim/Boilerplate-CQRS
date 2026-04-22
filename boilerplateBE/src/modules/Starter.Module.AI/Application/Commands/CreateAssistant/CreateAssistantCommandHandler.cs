using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

internal sealed class CreateAssistantCommandHandler(
    AiDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAssistantCommand, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        CreateAssistantCommand request,
        CancellationToken cancellationToken)
    {
        // Name uniqueness is scoped per tenant. Platform admins (TenantId=null) share the
        // "global" namespace; tenant users collide only within their own tenant.
        // IgnoreQueryFilters makes the scope explicit — otherwise a SuperAdmin create would
        // match across every tenant because the global filter passes for TenantId=null.
        var tenantId = currentUser.TenantId;
        var normalized = request.Name.Trim();

        var nameTaken = await context.AiAssistants
            .IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId && a.Name == normalized, cancellationToken);
        if (nameTaken)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNameAlreadyExists);

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
            isActive: true);

        if (request.EnabledToolNames is { Count: > 0 })
            assistant.SetEnabledTools(request.EnabledToolNames);

        if (request.KnowledgeBaseDocIds is { Count: > 0 })
            assistant.SetKnowledgeBase(request.KnowledgeBaseDocIds);

        if (request.RagScope != Domain.Enums.AiRagScope.None)
            assistant.SetRagScope(request.RagScope);

        context.AiAssistants.Add(assistant);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(assistant.ToDto());
    }
}
