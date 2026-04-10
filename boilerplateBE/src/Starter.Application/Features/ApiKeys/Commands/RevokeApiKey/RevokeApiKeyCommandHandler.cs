using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.ApiKeys.Errors;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.RevokeApiKey;

public sealed class RevokeApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<RevokeApiKeyCommand, Result>
{
    public async Task<Result> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.Set<ApiKey>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure(ApiKeyErrors.NotFound);

        if (apiKey.IsRevoked)
            return Result.Failure(ApiKeyErrors.AlreadyRevoked);

        if (apiKey.IsPlatformKey)
        {
            // Platform key: only platform admin with DeletePlatform can revoke
            if (currentUserService.TenantId.HasValue)
                return Result.Failure(ApiKeyErrors.NotFound); // Hide from tenant users

            if (!currentUserService.HasPermission(Starter.Shared.Constants.Permissions.ApiKeys.DeletePlatform))
                return Result.Failure(ApiKeyErrors.NotFound);
        }
        else
        {
            // Tenant key
            if (currentUserService.TenantId.HasValue)
            {
                // Tenant user: can only revoke own tenant's keys
                if (apiKey.TenantId != currentUserService.TenantId)
                    return Result.Failure(ApiKeyErrors.NotFound);
            }
            else
            {
                // Platform admin: cannot revoke tenant keys via normal endpoint
                return Result.Failure(ApiKeyErrors.UseTenantEmergencyRevoke);
            }
        }

        apiKey.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
