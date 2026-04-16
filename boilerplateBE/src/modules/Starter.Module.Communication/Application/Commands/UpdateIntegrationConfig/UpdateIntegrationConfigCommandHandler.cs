using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateIntegrationConfig;

internal sealed class UpdateIntegrationConfigCommandHandler(
    CommunicationDbContext dbContext,
    ICredentialEncryptionService encryptionService)
    : IRequestHandler<UpdateIntegrationConfigCommand, Result<IntegrationConfigDto>>
{
    public async Task<Result<IntegrationConfigDto>> Handle(
        UpdateIntegrationConfigCommand request,
        CancellationToken cancellationToken)
    {
        var config = await dbContext.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure<IntegrationConfigDto>(CommunicationErrors.IntegrationConfigNotFound);

        // Merge credentials: keep existing values for keys where the new value is empty
        var existingCredentials = encryptionService.Decrypt(config.CredentialsJson);
        var mergedCredentials = new Dictionary<string, string>(existingCredentials);

        foreach (var (key, value) in request.Credentials)
        {
            if (!string.IsNullOrWhiteSpace(value))
                mergedCredentials[key] = value;
        }

        var encryptedCredentials = encryptionService.Encrypt(mergedCredentials);
        config.Update(request.DisplayName, encryptedCredentials, request.ChannelMappingsJson);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(config.ToDto());
    }
}
