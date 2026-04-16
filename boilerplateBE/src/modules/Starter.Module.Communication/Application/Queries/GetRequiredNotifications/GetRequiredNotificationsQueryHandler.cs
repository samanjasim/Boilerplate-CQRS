using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetRequiredNotifications;

internal sealed class GetRequiredNotificationsQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetRequiredNotificationsQuery, Result<List<RequiredNotificationDto>>>
{
    public async Task<Result<List<RequiredNotificationDto>>> Handle(
        GetRequiredNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await context.RequiredNotifications
            .AsNoTracking()
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Channel)
            .ToListAsync(cancellationToken);

        return Result.Success(items.Select(r => r.ToDto()).ToList());
    }
}
