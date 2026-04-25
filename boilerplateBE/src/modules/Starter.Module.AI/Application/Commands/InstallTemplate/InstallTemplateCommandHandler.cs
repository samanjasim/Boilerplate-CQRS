using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.InstallTemplate;

internal sealed class InstallTemplateCommandHandler(
    AiDbContext db,
    IAiAgentTemplateRegistry templates,
    IAiToolRegistry tools,
    ICurrentUserService currentUser) : IRequestHandler<InstallTemplateCommand, Result<Guid>>
{
    private static readonly HashSet<string> SystemReservedPersonas =
        new(new[] { AiPersona.AnonymousSlug, AiPersona.DefaultSlug }, StringComparer.Ordinal);

    public async Task<Result<Guid>> Handle(
        InstallTemplateCommand request, CancellationToken ct)
    {
        // 1. Resolve target tenant
        var callerTenantId = currentUser.TenantId;
        var targetTenantId = request.TargetTenantId ?? callerTenantId;
        if (request.TargetTenantId is { } explicitTarget
            && explicitTarget != callerTenantId
            && !currentUser.IsInRole(Roles.SuperAdmin))
        {
            return Result<Guid>.Failure(TemplateErrors.Forbidden());
        }
        if (targetTenantId is null)
            return Result<Guid>.Failure(TemplateErrors.Forbidden());

        // 2. Resolve template
        var template = templates.Find(request.TemplateSlug);
        if (template is null)
            return Result<Guid>.Failure(TemplateErrors.NotFound(request.TemplateSlug));

        // 3. Duplicate-install guard (slug-collision in target tenant)
        var collision = await db.AiAssistants
            .IgnoreQueryFilters()
            .AnyAsync(
                a => a.TenantId == targetTenantId.Value && a.Slug == template.Slug,
                ct);
        if (collision)
            return Result<Guid>.Failure(
                TemplateErrors.AlreadyInstalled(template.Slug, targetTenantId.Value));

        // 4. Persona validation: every target slug must reference a real persona OR be system-reserved
        var tenantPersonas = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == targetTenantId.Value)
            .Select(p => p.Slug)
            .ToListAsync(ct);
        var availableSlugs = new HashSet<string>(
            tenantPersonas.Concat(SystemReservedPersonas), StringComparer.Ordinal);
        foreach (var slug in template.PersonaTargetSlugs)
        {
            if (!availableSlugs.Contains(slug))
                return Result<Guid>.Failure(TemplateErrors.PersonaTargetMissing(slug));
        }

        // 5. Tool validation: every tool slug must be registered
        foreach (var toolName in template.EnabledToolNames)
        {
            if (tools.FindByName(toolName) is null)
                return Result<Guid>.Failure(TemplateErrors.ToolMissing(toolName));
        }

        // 6. Resolve owner
        var ownerId = request.CreatedByUserIdOverride
            ?? currentUser.UserId
            ?? throw new InvalidOperationException(
                "InstallTemplateCommand requires either an authenticated user or CreatedByUserIdOverride.");

        // 7. Create assistant
        var assistant = AiAssistant.Create(
            tenantId: targetTenantId.Value,
            name: template.DisplayName,
            description: template.Description,
            systemPrompt: template.SystemPrompt,
            createdByUserId: ownerId,
            provider: template.Provider,
            model: template.Model,
            temperature: template.Temperature,
            maxTokens: template.MaxTokens,
            executionMode: template.ExecutionMode,
            maxAgentSteps: 10,
            isActive: true,
            slug: template.Slug);

        if (template.EnabledToolNames.Count > 0)
            assistant.SetEnabledTools(template.EnabledToolNames);
        if (template.PersonaTargetSlugs.Count > 0)
            assistant.SetPersonaTargets(template.PersonaTargetSlugs);
        assistant.SetVisibility(ResourceVisibility.TenantWide);

        // 8. Stamp provenance
        assistant.StampTemplateSource(template.Slug, version: null);

        // 9. Persist
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(assistant.Id);
    }
}
