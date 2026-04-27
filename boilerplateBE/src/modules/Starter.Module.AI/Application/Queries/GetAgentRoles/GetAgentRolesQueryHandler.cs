using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAgentRoles;

internal sealed class GetAgentRolesQueryHandler(
    AiDbContext aiDb,
    IApplicationDbContext appDb) : IRequestHandler<GetAgentRolesQuery, Result<IReadOnlyList<AgentRoleDto>>>
{
    public async Task<Result<IReadOnlyList<AgentRoleDto>>> Handle(GetAgentRolesQuery request, CancellationToken ct)
    {
        var principalId = await aiDb.AiAgentPrincipals
            .Where(p => p.AiAssistantId == request.AssistantId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (principalId == Guid.Empty)
            return Result.Failure<IReadOnlyList<AgentRoleDto>>(AiAgentErrors.AgentPrincipalNotFound(request.AssistantId));

        var assignments = await aiDb.AiAgentRoles
            .AsNoTracking()
            .Where(r => r.AgentPrincipalId == principalId)
            .Select(r => new { r.RoleId, r.AssignedAt })
            .ToListAsync(ct);

        if (assignments.Count == 0)
            return Result.Success<IReadOnlyList<AgentRoleDto>>(Array.Empty<AgentRoleDto>());

        var roleIds = assignments.Select(a => a.RoleId).ToList();
        var roleNames = await appDb.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var dtos = assignments
            .Select(a => new AgentRoleDto(
                a.RoleId,
                roleNames.GetValueOrDefault(a.RoleId, "(unknown)"),
                a.AssignedAt))
            .ToList();

        return Result.Success<IReadOnlyList<AgentRoleDto>>(dtos);
    }
}
