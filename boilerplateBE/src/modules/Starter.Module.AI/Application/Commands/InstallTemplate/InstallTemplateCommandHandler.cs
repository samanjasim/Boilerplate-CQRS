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
        // 1. Resolve target tenant.
        // The cross-tenant guard is bypassed for the seed path: when
        // CreatedByUserIdOverride is provided, the caller is an internal seed
        // actor (AIModule.SeedDataAsync passes Guid.Empty). The HTTP controller
        // never passes CreatedByUserIdOverride from request bodies, so this
        // bypass cannot be triggered from the network.
        var callerTenantId = currentUser.TenantId;
        var targetTenantId = request.TargetTenantId ?? callerTenantId;
        var isSystemSeedActor = request.CreatedByUserIdOverride.HasValue;
        if (!isSystemSeedActor
            && request.TargetTenantId is { } explicitTarget
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

        // 4. Name-collision guard: AiAssistant has a unique index on (TenantId, Name).
        // A tenant may have manually created an assistant whose Name matches the
        // template's DisplayName even with a different Slug — catch that cleanly
        // instead of letting EF throw a DbUpdateException at SaveChangesAsync.
        var nameTaken = await db.AiAssistants
            .IgnoreQueryFilters()
            .AnyAsync(
                a => a.TenantId == targetTenantId.Value && a.Name == template.DisplayName,
                ct);
        if (nameTaken)
            return Result<Guid>.Failure(
                TemplateErrors.AlreadyInstalled(template.Slug, targetTenantId.Value));

        // 5. Persona validation: every target slug must reference a real persona OR be system-reserved
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

        // 6. Tool validation: every tool slug must be registered
        foreach (var toolName in template.EnabledToolNames)
        {
            if (tools.FindByName(toolName) is null)
                return Result<Guid>.Failure(TemplateErrors.ToolMissing(toolName));
        }

        // 7. Resolve owner
        var ownerId = request.CreatedByUserIdOverride
            ?? currentUser.UserId
            ?? throw new InvalidOperationException(
                "InstallTemplateCommand requires either an authenticated user or CreatedByUserIdOverride.");

        // 8. Create assistant
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

        // 9. Stamp provenance
        assistant.StampTemplateSource(template.Slug, version: null);

        // 10. Persist
        db.AiAssistants.Add(assistant);

        // Plan 5d-1: pair with an AiAgentPrincipal in the same EF transaction so the
        // installed template is immediately operable as a security subject.
        if (assistant.TenantId is { } tenantId)
        {
            var principal = AiAgentPrincipal.Create(assistant.Id, tenantId, assistant.IsActive);
            db.AiAgentPrincipals.Add(principal);
        }

        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(assistant.Id);
    }
}
