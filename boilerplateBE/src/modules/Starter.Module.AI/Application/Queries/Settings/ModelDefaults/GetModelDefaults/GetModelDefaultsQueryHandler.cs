using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.ModelDefaults.GetModelDefaults;

internal sealed class GetModelDefaultsQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetModelDefaultsQuery, Result<IReadOnlyList<AiModelDefaultDto>>>
{
    public async Task<Result<IReadOnlyList<AiModelDefaultDto>>> Handle(GetModelDefaultsQuery request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<IReadOnlyList<AiModelDefaultDto>>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to get AI model defaults."));

        var rows = await db.AiModelDefaults
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId.Value)
            .OrderBy(d => d.AgentClass)
            .Select(d => new AiModelDefaultDto(
                d.Id,
                d.TenantId,
                d.AgentClass,
                d.Provider,
                d.Model,
                d.MaxTokens,
                d.Temperature))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<AiModelDefaultDto>>(rows);
    }
}
