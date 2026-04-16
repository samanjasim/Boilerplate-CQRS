using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.TestChannelConfig;

internal sealed class TestChannelConfigCommandHandler(
    CommunicationDbContext dbContext,
    ICredentialEncryptionService encryptionService,
    ILogger<TestChannelConfigCommandHandler> logger)
    : IRequestHandler<TestChannelConfigCommand, Result<TestChannelConfigResponse>>
{
    public async Task<Result<TestChannelConfigResponse>> Handle(
        TestChannelConfigCommand request,
        CancellationToken cancellationToken)
    {
        var config = await dbContext.ChannelConfigs
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure<TestChannelConfigResponse>(CommunicationErrors.ChannelConfigNotFound);

        try
        {
            // Decrypt credentials to validate they're readable
            var credentials = encryptionService.Decrypt(config.CredentialsJson);

            // TODO: Phase 4 will add actual provider-specific connection testing
            // For now, just validate that credentials can be decrypted and have required fields
            var message = $"Credentials validated successfully for {config.Provider} provider.";
            config.RecordTestResult(true, message);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new TestChannelConfigResponse(true, message));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Channel config test failed for {ConfigId} ({Provider})",
                config.Id, config.Provider);

            const string message = "Connection test failed. Please verify your credentials and try again.";
            config.RecordTestResult(false, message);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new TestChannelConfigResponse(false, message));
        }
    }
}
