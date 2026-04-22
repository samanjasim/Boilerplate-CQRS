using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SetAssistantAccessMode;

internal sealed class SetAssistantAccessModeCommandHandler(
    AiDbContext context,
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
        return Result.Success();
    }
}
