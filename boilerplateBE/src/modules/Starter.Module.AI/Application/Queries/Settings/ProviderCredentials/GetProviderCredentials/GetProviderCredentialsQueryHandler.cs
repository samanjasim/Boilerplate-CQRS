using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.ProviderCredentials.GetProviderCredentials;

internal sealed class GetProviderCredentialsQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    IAiSecretProtector secrets) : IRequestHandler<GetProviderCredentialsQuery, Result<IReadOnlyList<AiProviderCredentialDto>>>
{
    public async Task<Result<IReadOnlyList<AiProviderCredentialDto>>> Handle(
        GetProviderCredentialsQuery request,
        CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<IReadOnlyList<AiProviderCredentialDto>>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to read AI provider credentials."));

        var credentials = await db.AiProviderCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId.Value)
            .OrderBy(c => c.Provider)
            .ThenBy(c => c.DisplayName)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<AiProviderCredentialDto>>(
            credentials.Select(c => AiProviderCredentialDtos.ToDto(c, secrets)).ToList());
    }
}
