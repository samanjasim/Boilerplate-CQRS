using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetIntegrationConfigs;

internal sealed class GetIntegrationConfigsQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetIntegrationConfigsQuery, Result<List<IntegrationConfigDto>>>
{
    public async Task<Result<List<IntegrationConfigDto>>> Handle(
        GetIntegrationConfigsQuery request,
        CancellationToken cancellationToken)
    {
        var configs = await context.IntegrationConfigs
            .AsNoTracking()
            .OrderBy(c => c.IntegrationType)
            .ThenBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);

        return Result.Success(configs.Select(c => c.ToDto()).ToList());
    }
}
