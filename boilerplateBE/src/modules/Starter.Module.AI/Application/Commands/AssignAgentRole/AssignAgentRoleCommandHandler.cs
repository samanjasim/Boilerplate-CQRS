using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.AssignAgentRole;

internal sealed class AssignAgentRoleCommandHandler(
    AiDbContext aiDb,
    IApplicationDbContext appDb,
    ICurrentUserService currentUser) : IRequestHandler<AssignAgentRoleCommand, Result>
{
    public async Task<Result> Handle(AssignAgentRoleCommand request, CancellationToken ct)
    {
        // Resolve the agent principal for this assistant.
        var principal = await aiDb.AiAgentPrincipals
            .FirstOrDefaultAsync(p => p.AiAssistantId == request.AssistantId, ct);
        if (principal is null)
            return Result.Failure(AiAgentErrors.AgentPrincipalNotFound(request.AssistantId));

        // Verify role exists in core.
        var roleExists = await appDb.Roles
            .IgnoreQueryFilters()
            .AnyAsync(r => r.Id == request.RoleId, ct);
        if (!roleExists)
            return Result.Failure(Error.NotFound("Role.NotFound", $"Role '{request.RoleId}' not found."));

        // Plan 5d-1: AiRoleMetadata gates which roles can be assigned to agents.
        // Default behaviour when no row exists: NOT assignable (fail-closed). The seed
        // inserts an explicit row for every core role; locked roles (SuperAdmin, TenantAdmin)
        // are seeded with IsAgentAssignable=false. A new core role created at runtime
        // requires an explicit AiRoleMetadata insert before it can be assigned to agents,
        // matching the "explicit allow" pattern used elsewhere in the security model.
        var meta = await aiDb.AiRoleMetadataEntries
            .FirstOrDefaultAsync(m => m.RoleId == request.RoleId, ct);
        if (meta is null || !meta.IsAgentAssignable)
            return Result.Failure(AiAgentErrors.AgentRoleAssignmentNotPermitted(request.RoleId));

        // Idempotent: if assignment already exists, return success.
        var existing = await aiDb.AiAgentRoles
            .AnyAsync(r => r.AgentPrincipalId == principal.Id && r.RoleId == request.RoleId, ct);
        if (existing) return Result.Success();

        var assigner = currentUser.UserId ?? Guid.Empty;
        aiDb.AiAgentRoles.Add(AiAgentRole.Create(principal.Id, request.RoleId, assigner));
        await aiDb.SaveChangesAsync(ct);
        return Result.Success();
    }
}
