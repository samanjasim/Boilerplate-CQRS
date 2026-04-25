using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.Messages;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;

namespace Starter.Module.Communication.Infrastructure.Services;

internal sealed class MessageDispatcher(
    CommunicationDbContext dbContext,
    ITemplateEngine templateEngine,
    ICurrentUserService currentUserService,
    IRecipientResolver recipientResolver,
    IIntegrationEventCollector eventCollector,
    ILogger<MessageDispatcher> logger) : IMessageDispatcher
{
    public async Task<Guid> SendAsync(
        string templateName,
        Guid recipientUserId,
        Dictionary<string, object> variables,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? currentUserService.TenantId;
        if (!resolvedTenantId.HasValue)
        {
            logger.LogWarning("Cannot dispatch message — no tenant context (template: {Template})", templateName);
            return Guid.Empty;
        }

        return await DispatchCoreAsync(
            templateName, recipientUserId, variables, resolvedTenantId.Value,
            channelOverride: null, checkPreferences: true, cancellationToken);
    }

    public async Task<Guid> SendToChannelAsync(
        string templateName,
        Guid recipientUserId,
        NotificationChannelType channel,
        Dictionary<string, object> variables,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var moduleChannel = (NotificationChannel)(int)channel;

        var resolvedTenantId = tenantId ?? currentUserService.TenantId;
        if (!resolvedTenantId.HasValue)
        {
            logger.LogWarning("Cannot dispatch message — no tenant context (template: {Template})", templateName);
            return Guid.Empty;
        }

        return await DispatchCoreAsync(
            templateName, recipientUserId, variables, resolvedTenantId.Value,
            channelOverride: moduleChannel, checkPreferences: false, cancellationToken);
    }

    private async Task<Guid> DispatchCoreAsync(
        string templateName,
        Guid recipientUserId,
        Dictionary<string, object> variables,
        Guid resolvedTenantId,
        NotificationChannel? channelOverride,
        bool checkPreferences,
        CancellationToken cancellationToken)
    {
        // Resolve template
        var template = await dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == templateName, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Message template '{TemplateName}' not found", templateName);
            return Guid.Empty;
        }

        // Check for tenant override
        var tenantOverride = await dbContext.MessageTemplateOverrides
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.MessageTemplateId == template.Id
                && o.TenantId == resolvedTenantId
                && o.IsActive, cancellationToken);

        var subjectTemplate = tenantOverride?.SubjectTemplate ?? template.SubjectTemplate;
        var bodyTemplate = tenantOverride?.BodyTemplate ?? template.BodyTemplate;

        // Render
        string? renderedSubject = null;
        string renderedBody;
        try
        {
            renderedSubject = subjectTemplate is not null ? templateEngine.Render(subjectTemplate, variables) : null;
            renderedBody = templateEngine.Render(bodyTemplate, variables);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to render template '{TemplateName}'", templateName);
            return Guid.Empty;
        }

        // Determine channel
        var channel = channelOverride ?? template.DefaultChannel;

        // Check notification preferences if needed (only for SendAsync, not SendToChannelAsync)
        if (checkPreferences)
        {
            var preference = await dbContext.NotificationPreferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == recipientUserId
                    && p.TenantId == resolvedTenantId
                    && p.Category == template.Category, cancellationToken);

            var isRequired = await dbContext.RequiredNotifications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(r => r.TenantId == resolvedTenantId
                    && r.Category == template.Category
                    && (int)r.Channel == (int)channel, cancellationToken);

            if (preference is not null && !isRequired && !IsChannelEnabled(preference, channel))
            {
                logger.LogDebug(
                    "User {UserId} opted out of {Channel} for category {Category}, falling back to InApp",
                    recipientUserId, channel, template.Category);
                channel = NotificationChannel.InApp;
            }
        }

        // Resolve recipient address
        var recipientAddress = await recipientResolver.ResolveAddressAsync(
            recipientUserId, channel, cancellationToken);

        if (string.IsNullOrWhiteSpace(recipientAddress))
        {
            if (channel == NotificationChannel.Email
                && variables.TryGetValue("email", out var emailObj)
                && emailObj is string email
                && !string.IsNullOrWhiteSpace(email))
            {
                recipientAddress = email;
            }
            else
            {
                channel = NotificationChannel.InApp;
                recipientAddress = recipientUserId.ToString();
            }
        }

        // Build fallback chain
        string[] fallbackChannels;
        if (channelOverride.HasValue)
        {
            // Explicit channel sends: InApp as only fallback
            fallbackChannels = channelOverride.Value == NotificationChannel.InApp ? [] : ["InApp"];
        }
        else
        {
            // Default sends: available channels minus the primary, ending with InApp
            var availableChannels = string.IsNullOrWhiteSpace(template.AvailableChannelsJson)
                ? []
                : JsonSerializer.Deserialize<string[]>(template.AvailableChannelsJson) ?? [];

            fallbackChannels = availableChannels
                .Where(c => c != channel.ToString() && c != "InApp")
                .Append("InApp")
                .Distinct()
                .ToArray();
        }

        // Create delivery log
        var deliveryLog = DeliveryLog.Create(
            resolvedTenantId,
            recipientUserId,
            recipientAddress,
            template.Id,
            templateName,
            channel,
            null,
            renderedSubject,
            renderedBody,
            JsonSerializer.Serialize(variables));

        dbContext.DeliveryLogs.Add(deliveryLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Schedule on the request-scoped collector. The originating handler's
        // ApplicationDbContext.SaveChangesAsync runs the IntegrationEventOutboxInterceptor
        // which writes the outbox row atomically — even though this dispatcher
        // operates on its own CommunicationDbContext, the interceptor lives on
        // the appDb side and shares the DI scope.
        eventCollector.Schedule(new DispatchMessageMessage(
            deliveryLog.Id,
            resolvedTenantId,
            recipientUserId,
            recipientAddress,
            templateName,
            renderedSubject,
            renderedBody,
            channel,
            fallbackChannels,
            -1,
            DateTime.UtcNow));

        logger.LogInformation(
            "Message queued: DeliveryLog={Id}, Template={Template}, Channel={Channel}, Recipient={UserId}",
            deliveryLog.Id, templateName, channel, recipientUserId);

        return deliveryLog.Id;
    }

    private static bool IsChannelEnabled(
        Domain.Entities.CommunicationNotificationPreference pref,
        NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => pref.EmailEnabled,
        NotificationChannel.Sms => pref.SmsEnabled,
        NotificationChannel.Push => pref.PushEnabled,
        NotificationChannel.WhatsApp => pref.WhatsAppEnabled,
        NotificationChannel.InApp => true, // InApp always enabled
        _ => true,
    };
}
