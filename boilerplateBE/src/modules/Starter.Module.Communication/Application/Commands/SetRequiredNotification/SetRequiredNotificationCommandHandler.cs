using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.SetRequiredNotification;

internal sealed class SetRequiredNotificationCommandHandler(
    CommunicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<SetRequiredNotificationCommand, Result<RequiredNotificationDto>>
{
    public async Task<Result<RequiredNotificationDto>> Handle(
        SetRequiredNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<RequiredNotificationDto>(CommunicationErrors.TenantRequired);

        var exists = await dbContext.RequiredNotifications
            .AnyAsync(r => r.Category == request.Category && r.Channel == request.Channel,
                cancellationToken);
        if (exists)
            return Result.Failure<RequiredNotificationDto>(CommunicationErrors.DuplicateRequiredNotification);

        var entity = RequiredNotification.Create(tenantId.Value, request.Category, request.Channel);
        dbContext.RequiredNotifications.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.ToDto());
    }
}
