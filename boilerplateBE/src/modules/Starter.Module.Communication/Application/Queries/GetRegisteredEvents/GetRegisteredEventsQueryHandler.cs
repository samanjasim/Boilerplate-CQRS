using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetRegisteredEvents;

internal sealed class GetRegisteredEventsQueryHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<GetRegisteredEventsQuery, Result<List<EventRegistrationDto>>>
{
    public async Task<Result<List<EventRegistrationDto>>> Handle(
        GetRegisteredEventsQuery request,
        CancellationToken cancellationToken)
    {
        var events = await dbContext.EventRegistrations
            .AsNoTracking()
            .OrderBy(e => e.ModuleSource)
            .ThenBy(e => e.EventName)
            .ToListAsync(cancellationToken);

        return Result.Success(events.Select(e => e.ToDto()).ToList());
    }
}
