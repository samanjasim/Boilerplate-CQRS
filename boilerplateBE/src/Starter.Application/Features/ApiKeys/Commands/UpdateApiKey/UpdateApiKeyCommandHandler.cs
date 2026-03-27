using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.UpdateApiKey;

public sealed class UpdateApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateApiKeyCommand, Result<ApiKeyDto>>
{
    public async Task<Result<ApiKeyDto>> Handle(UpdateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);

        if (apiKey.IsPlatformKey)
        {
            // Platform key: only platform admin with UpdatePlatform
            if (currentUserService.TenantId.HasValue)
                return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);

            if (!currentUserService.HasPermission(Starter.Shared.Constants.Permissions.ApiKeys.UpdatePlatform))
                return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
        }
        else
        {
            // Tenant key
            if (currentUserService.TenantId.HasValue)
            {
                if (apiKey.TenantId != currentUserService.TenantId)
                    return Result.Failure<ApiKeyDto>(ApiKeyErrors.NotFound);
            }
            else
            {
                // Platform admin cannot modify tenant keys
                return Result.Failure<ApiKeyDto>(ApiKeyErrors.CannotModifyTenantKey);
            }
        }

        apiKey.UpdateDetails(request.Name, request.Scopes);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(apiKey.ToDto());
    }
}
