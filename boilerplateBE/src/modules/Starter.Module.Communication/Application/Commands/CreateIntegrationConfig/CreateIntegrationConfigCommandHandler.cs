using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.CreateIntegrationConfig;

internal sealed class CreateIntegrationConfigCommandHandler(
    CommunicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ICredentialEncryptionService encryptionService)
    : IRequestHandler<CreateIntegrationConfigCommand, Result<IntegrationConfigDto>>
{
    public async Task<Result<IntegrationConfigDto>> Handle(
        CreateIntegrationConfigCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<IntegrationConfigDto>(CommunicationErrors.TenantRequired);

        var encryptedCredentials = encryptionService.Encrypt(request.Credentials);

        var config = IntegrationConfig.Create(
            tenantId.Value,
            request.IntegrationType,
            request.DisplayName,
            encryptedCredentials);

        if (!string.IsNullOrWhiteSpace(request.ChannelMappingsJson))
            config.Update(request.DisplayName, encryptedCredentials, request.ChannelMappingsJson);

        dbContext.IntegrationConfigs.Add(config);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(config.ToDto());
    }
}
