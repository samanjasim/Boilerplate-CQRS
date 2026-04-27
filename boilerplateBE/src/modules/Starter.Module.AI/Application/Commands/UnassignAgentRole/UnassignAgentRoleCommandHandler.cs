using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UnassignAgentRole;

internal sealed class UnassignAgentRoleCommandHandler(AiDbContext db)
    : IRequestHandler<UnassignAgentRoleCommand, Result>
{
    public async Task<Result> Handle(UnassignAgentRoleCommand request, CancellationToken ct)
    {
        var principal = await db.AiAgentPrincipals
            .FirstOrDefaultAsync(p => p.AiAssistantId == request.AssistantId, ct);
        if (principal is null)
            return Result.Failure(AiAgentErrors.AgentPrincipalNotFound(request.AssistantId));

        var assignment = await db.AiAgentRoles
            .FirstOrDefaultAsync(r => r.AgentPrincipalId == principal.Id && r.RoleId == request.RoleId, ct);
        if (assignment is null)
            return Result.Success(); // idempotent

        db.AiAgentRoles.Remove(assignment);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
