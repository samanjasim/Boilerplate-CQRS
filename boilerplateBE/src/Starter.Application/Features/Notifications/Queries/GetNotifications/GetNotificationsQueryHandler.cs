using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Notifications.DTOs;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Notifications.Queries.GetNotifications;

internal sealed class GetNotificationsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetNotificationsQuery, Result<PaginatedList<NotificationDto>>>
{
    public async Task<Result<PaginatedList<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<PaginatedList<NotificationDto>>(UserErrors.Unauthorized());

        var query = context.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value);

        if (request.IsRead.HasValue)
            query = query.Where(n => n.IsRead == request.IsRead.Value);

        query = query.OrderByDescending(n => n.CreatedAt);

        var projectedQuery = query.Select(n => new NotificationDto(
            n.Id,
            n.Type,
            n.Title,
            n.Message,
            n.Data,
            n.IsRead,
            n.CreatedAt));

        var paginatedList = await PaginatedList<NotificationDto>.CreateAsync(
            projectedQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(paginatedList);
    }
}
