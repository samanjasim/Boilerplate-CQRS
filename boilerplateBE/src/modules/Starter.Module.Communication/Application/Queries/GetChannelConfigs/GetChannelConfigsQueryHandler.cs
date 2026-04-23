using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetChannelConfigs;

internal sealed class GetChannelConfigsQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetChannelConfigsQuery, Result<List<ChannelConfigDto>>>
{
    public async Task<Result<List<ChannelConfigDto>>> Handle(
        GetChannelConfigsQuery request,
        CancellationToken cancellationToken)
    {
        var configs = await context.ChannelConfigs
            .AsNoTracking()
            .OrderBy(c => c.Channel)
            .ThenByDescending(c => c.IsDefault)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result.Success(configs.Select(c => c.ToDto()).ToList());
    }
}
