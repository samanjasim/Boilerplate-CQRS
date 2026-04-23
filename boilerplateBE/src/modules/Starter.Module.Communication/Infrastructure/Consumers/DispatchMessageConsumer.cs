using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Application.Messages;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Providers;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Infrastructure.Consumers;

public sealed class DispatchMessageConsumer(
    CommunicationDbContext dbContext,
    IChannelProviderFactory providerFactory,
    ICredentialEncryptionService encryptionService,
    ILogger<DispatchMessageConsumer> logger)
    : IConsumer<DispatchMessageMessage>
{
    public async Task Consume(ConsumeContext<DispatchMessageMessage> context)
    {
        var msg = context.Message;

        logger.LogInformation(
            "Processing message dispatch: DeliveryLog={DeliveryLogId}, Channel={Channel}, Recipient={Recipient}",
            msg.DeliveryLogId, msg.Channel, msg.RecipientAddress);

        // Load delivery log with attempts for accurate attempt numbering
        var deliveryLog = await dbContext.DeliveryLogs
            .IgnoreQueryFilters()
            .Include(d => d.Attempts)
            .FirstOrDefaultAsync(d => d.Id == msg.DeliveryLogId, context.CancellationToken);

        if (deliveryLog is null)
        {
            logger.LogWarning("DeliveryLog {Id} not found, skipping dispatch", msg.DeliveryLogId);
            return;
        }

        // For InApp, no channel config needed — it's platform-managed
        Dictionary<string, string> credentials;
        Domain.Enums.ChannelProvider providerType;

        if (msg.Channel == NotificationChannel.InApp)
        {
            credentials = new Dictionary<string, string>();
            providerType = Domain.Enums.ChannelProvider.Ably;
        }
        else
        {
            // Load the tenant's default channel config for this channel
            var channelConfig = await dbContext.ChannelConfigs
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == msg.TenantId
                    && c.Channel == msg.Channel
                    && c.Status == ChannelConfigStatus.Active
                    && c.IsDefault)
                .FirstOrDefaultAsync(context.CancellationToken);

            // If no default, try any active config for this channel
            channelConfig ??= await dbContext.ChannelConfigs
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == msg.TenantId
                    && c.Channel == msg.Channel
                    && c.Status == ChannelConfigStatus.Active)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (channelConfig is null)
            {
                logger.LogWarning(
                    "No active channel config for {Channel} in tenant {TenantId}, trying fallback",
                    msg.Channel, msg.TenantId);

                deliveryLog.MarkFailed("No channel configuration found");
                await dbContext.SaveChangesAsync(context.CancellationToken);
                await PublishFallbackAsync(context, msg, deliveryLog);
                return;
            }

            credentials = encryptionService.Decrypt(channelConfig.CredentialsJson);
            providerType = channelConfig.Provider;
        }

        // Get the provider implementation
        var provider = providerFactory.GetProvider(msg.Channel, providerType);
        if (provider is null)
        {
            logger.LogWarning("No provider implementation for {Channel}/{Provider}", msg.Channel, providerType);
            deliveryLog.MarkFailed($"No provider implementation for {providerType}");
            await dbContext.SaveChangesAsync(context.CancellationToken);
            await PublishFallbackAsync(context, msg, deliveryLog);
            return;
        }

        // Create delivery attempt
        var attemptNumber = deliveryLog.Attempts.Count + 1;
        var attempt = DeliveryAttempt.Create(
            msg.DeliveryLogId, attemptNumber, msg.Channel, null, providerType);

        deliveryLog.MarkSending(providerType);

        // Send
        var request = new ChannelDeliveryRequest(
            msg.RecipientAddress, msg.RenderedSubject, msg.RenderedBody, credentials);

        var result = await provider.SendAsync(request, context.CancellationToken);

        if (result.Success)
        {
            attempt.RecordSuccess(result.ProviderMessageId, result.DurationMs);
            deliveryLog.MarkDelivered(result.ProviderMessageId, result.DurationMs);
            logger.LogInformation(
                "Message delivered: DeliveryLog={Id}, Channel={Channel}, Duration={Duration}ms",
                msg.DeliveryLogId, msg.Channel, result.DurationMs);
        }
        else
        {
            attempt.RecordFailure(result.ErrorMessage, result.ProviderMessageId, result.DurationMs);
            deliveryLog.MarkFailed(result.ErrorMessage);
            logger.LogWarning(
                "Message delivery failed: DeliveryLog={Id}, Channel={Channel}, Error={Error}",
                msg.DeliveryLogId, msg.Channel, result.ErrorMessage);
        }

        deliveryLog.AddAttempt(attempt);
        dbContext.DeliveryAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        // If delivery failed, try fallback
        if (!result.Success)
        {
            await PublishFallbackAsync(context, msg, deliveryLog);
        }
    }

    private async Task PublishFallbackAsync(
        ConsumeContext<DispatchMessageMessage> context,
        DispatchMessageMessage msg,
        DeliveryLog deliveryLog)
    {
        var nextIndex = msg.CurrentFallbackIndex + 1;

        if (nextIndex < msg.FallbackChannels.Length)
        {
            var nextChannelStr = msg.FallbackChannels[nextIndex];
            if (Enum.TryParse<NotificationChannel>(nextChannelStr, out var nextChannel))
            {
                logger.LogInformation(
                    "Falling back to {Channel} for DeliveryLog={Id}",
                    nextChannel, msg.DeliveryLogId);

                await context.Publish(new DispatchMessageMessage(
                    msg.DeliveryLogId,
                    msg.TenantId,
                    msg.RecipientUserId,
                    msg.RecipientAddress,
                    msg.TemplateName,
                    msg.RenderedSubject,
                    msg.RenderedBody,
                    nextChannel,
                    msg.FallbackChannels,
                    nextIndex,
                    DateTime.UtcNow));
                return;
            }
        }

        // No more fallbacks — try InApp as final implicit fallback
        if (msg.Channel != NotificationChannel.InApp)
        {
            logger.LogInformation(
                "All fallbacks exhausted, delivering via InApp for DeliveryLog={Id}",
                msg.DeliveryLogId);

            await context.Publish(new DispatchMessageMessage(
                msg.DeliveryLogId,
                msg.TenantId,
                msg.RecipientUserId,
                msg.RecipientAddress,
                msg.TemplateName,
                msg.RenderedSubject,
                msg.RenderedBody,
                NotificationChannel.InApp,
                [],
                -1,
                DateTime.UtcNow));
        }
    }
}
