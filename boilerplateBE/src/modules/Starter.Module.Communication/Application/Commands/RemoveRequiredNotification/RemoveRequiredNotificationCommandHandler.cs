using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.RemoveRequiredNotification;

internal sealed class RemoveRequiredNotificationCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<RemoveRequiredNotificationCommand, Result>
{
    public async Task<Result> Handle(
        RemoveRequiredNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.RequiredNotifications
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result.Failure(CommunicationErrors.RequiredNotificationNotFound);

        dbContext.RequiredNotifications.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
