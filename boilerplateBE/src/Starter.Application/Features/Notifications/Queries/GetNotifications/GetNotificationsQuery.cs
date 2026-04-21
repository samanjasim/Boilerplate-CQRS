using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.Notifications.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery : PaginationQuery, IRequest<Result<PaginatedList<NotificationDto>>>
{
    public bool? IsRead { get; init; }
}
