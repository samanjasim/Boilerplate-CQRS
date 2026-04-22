using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SetAssistantAccessMode;

internal sealed class SetAssistantAccessModeCommandHandler(
    AiDbContext context,
    IApplicationDbContext coreDb,
    IResourceAccessService access,
    ICurrentUserService currentUser)
    : IRequestHandler<SetAssistantAccessModeCommand, Result>
{
    public async Task<Result> Handle(
        SetAssistantAccessModeCommand request,
        CancellationToken cancellationToken)
    {
        var assistant = await context.AiAssistants
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (assistant is null)
            return Result.Failure(AiErrors.AssistantNotFound);

        // Only the owner (or admin bypass) may change access-mode.
        var canManage = await access.CanAccessAsync(
            currentUser, ResourceTypes.AiAssistant, assistant.Id, AccessLevel.Manager, cancellationToken);
        if (!canManage)
            return Result.Failure(AiErrors.AssistantNotFound);

        assistant.SetAccessMode(request.AccessMode);
        await context.SaveChangesAsync(cancellationToken);

        coreDb.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.AiAssistant,
            EntityId = assistant.Id,
            Action = AuditAction.Updated,
            Changes = JsonSerializer.Serialize(new
            {
                Event = "AssistantAccessModeChanged",
                AssistantId = assistant.Id,
                AccessMode = request.AccessMode.ToString()
            }),
            PerformedBy = currentUser.UserId,
            PerformedByName = currentUser.Email,
            PerformedAt = DateTime.UtcNow,
            TenantId = currentUser.TenantId
        });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
