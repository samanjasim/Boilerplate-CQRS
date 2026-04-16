using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetIntegrationConfigById;

internal sealed class GetIntegrationConfigByIdQueryHandler(
    CommunicationDbContext context,
    ICredentialEncryptionService encryptionService)
    : IRequestHandler<GetIntegrationConfigByIdQuery, Result<IntegrationConfigDto>>
{
    public async Task<Result<IntegrationConfigDto>> Handle(
        GetIntegrationConfigByIdQuery request,
        CancellationToken cancellationToken)
    {
        var config = await context.IntegrationConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure<IntegrationConfigDto>(CommunicationErrors.IntegrationConfigNotFound);

        var credentials = encryptionService.Decrypt(config.CredentialsJson);
        var maskedCredentials = encryptionService.Mask(credentials);

        return Result.Success(config.ToDto(maskedCredentials));
    }
}
