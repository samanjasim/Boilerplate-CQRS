using System.Diagnostics;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Application.Messages;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Providers;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Infrastructure.Consumers;

internal sealed class DispatchIntegrationConsumer(
    CommunicationDbContext dbContext,
    IIntegrationProviderFactory providerFactory,
    ICredentialEncryptionService encryptionService,
    ILogger<DispatchIntegrationConsumer> logger)
    : IConsumer<DispatchIntegrationMessage>
{
    public async Task Consume(ConsumeContext<DispatchIntegrationMessage> context)
    {
        var msg = context.Message;

        logger.LogInformation(
            "Processing integration dispatch: DeliveryLog={DeliveryLogId}, IntegrationConfig={ConfigId}",
            msg.DeliveryLogId, msg.IntegrationConfigId);

        // Load delivery log
        var deliveryLog = await dbContext.DeliveryLogs
            .IgnoreQueryFilters()
            .Include(d => d.Attempts)
            .FirstOrDefaultAsync(d => d.Id == msg.DeliveryLogId, context.CancellationToken);

        if (deliveryLog is null)
        {
            logger.LogWarning("DeliveryLog {Id} not found, skipping integration dispatch", msg.DeliveryLogId);
            return;
        }

        // Load integration config
        var integrationConfig = await dbContext.IntegrationConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == msg.IntegrationConfigId, context.CancellationToken);

        if (integrationConfig is null)
        {
            logger.LogWarning("IntegrationConfig {Id} not found, marking delivery failed", msg.IntegrationConfigId);
            deliveryLog.MarkFailed("Integration configuration not found.");
            await dbContext.SaveChangesAsync(context.CancellationToken);
            return;
        }

        // Get provider
        var provider = providerFactory.GetProvider(integrationConfig.IntegrationType);
        if (provider is null)
        {
            logger.LogWarning("No provider for {Type}", integrationConfig.IntegrationType);
            deliveryLog.MarkFailed($"No provider implementation for {integrationConfig.IntegrationType}");
            await dbContext.SaveChangesAsync(context.CancellationToken);
            return;
        }

        // Decrypt credentials
        var credentials = encryptionService.Decrypt(integrationConfig.CredentialsJson);

        // Create delivery attempt with IntegrationType set
        var attemptNumber = deliveryLog.Attempts.Count + 1;
        var attempt = DeliveryAttempt.Create(
            msg.DeliveryLogId, attemptNumber, null, integrationConfig.IntegrationType, null);

        // Send
        var request = new IntegrationDeliveryRequest(
            msg.TargetChannelId, msg.Message, credentials);

        var result = await provider.SendAsync(request, context.CancellationToken);

        if (result.Success)
        {
            attempt.RecordSuccess(result.ProviderMessageId, result.DurationMs);
            deliveryLog.MarkDelivered(result.ProviderMessageId, result.DurationMs);
            logger.LogInformation(
                "Integration message delivered: DeliveryLog={Id}, Type={Type}, Duration={Duration}ms",
                msg.DeliveryLogId, integrationConfig.IntegrationType, result.DurationMs);
        }
        else
        {
            attempt.RecordFailure(result.ErrorMessage, result.ProviderMessageId, result.DurationMs);
            deliveryLog.MarkFailed(result.ErrorMessage);
            logger.LogWarning(
                "Integration delivery failed: DeliveryLog={Id}, Type={Type}, Error={Error}",
                msg.DeliveryLogId, integrationConfig.IntegrationType, result.ErrorMessage);
        }

        deliveryLog.AddAttempt(attempt);
        dbContext.DeliveryAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
