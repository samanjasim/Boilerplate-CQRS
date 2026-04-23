using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Providers;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.TestIntegrationConfig;

internal sealed class TestIntegrationConfigCommandHandler(
    CommunicationDbContext dbContext,
    ICredentialEncryptionService encryptionService,
    IIntegrationProviderFactory providerFactory,
    ILogger<TestIntegrationConfigCommandHandler> logger)
    : IRequestHandler<TestIntegrationConfigCommand, Result<TestIntegrationConfigResponse>>
{
    public async Task<Result<TestIntegrationConfigResponse>> Handle(
        TestIntegrationConfigCommand request,
        CancellationToken cancellationToken)
    {
        var config = await dbContext.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure<TestIntegrationConfigResponse>(CommunicationErrors.IntegrationConfigNotFound);

        try
        {
            var credentials = encryptionService.Decrypt(config.CredentialsJson);

            var provider = providerFactory.GetProvider(config.IntegrationType);
            if (provider is null)
            {
                const string noProvider = "No provider implementation available for this integration type.";
                config.RecordTestResult(false, noProvider);
                await dbContext.SaveChangesAsync(cancellationToken);
                return Result.Success(new TestIntegrationConfigResponse(false, noProvider));
            }

            // Send a test message
            var testRequest = new IntegrationDeliveryRequest(
                TargetChannelId: "test",
                Message: "Test connection from Communication module.",
                ProviderCredentials: credentials);

            var result = await provider.SendAsync(testRequest, cancellationToken);

            var message = result.Success
                ? $"Connection test passed for {config.IntegrationType} integration."
                : "Connection test failed. Please verify your credentials and try again.";

            config.RecordTestResult(result.Success, message);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new TestIntegrationConfigResponse(result.Success, message));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Integration config test failed for {ConfigId} ({Type})",
                config.Id, config.IntegrationType);

            const string message = "Connection test failed. Please verify your credentials and try again.";
            config.RecordTestResult(false, message);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new TestIntegrationConfigResponse(false, message));
        }
    }
}
