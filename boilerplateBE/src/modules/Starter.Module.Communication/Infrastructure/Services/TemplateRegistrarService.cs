using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;

namespace Starter.Module.Communication.Infrastructure.Services;

internal sealed class TemplateRegistrarService(
    CommunicationDbContext dbContext,
    ILogger<TemplateRegistrarService> logger) : ITemplateRegistrar
{
    public async Task RegisterTemplateAsync(
        string name, string moduleSource, string category, string? description,
        string? subjectTemplate, string bodyTemplate,
        NotificationChannelType defaultChannel, string[] availableChannels,
        Dictionary<string, string>? variableSchema = null,
        Dictionary<string, object>? sampleVariables = null,
        CancellationToken ct = default)
    {
        var exists = await dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Name == name, ct);

        if (exists)
        {
            logger.LogDebug("Template '{Name}' already registered, skipping", name);
            return;
        }

        // Map from Abstractions enum to module enum
        var moduleChannel = (NotificationChannel)(int)defaultChannel;

        var template = MessageTemplate.Create(
            name, moduleSource, category, description,
            subjectTemplate, bodyTemplate, moduleChannel,
            JsonSerializer.Serialize(availableChannels),
            variableSchema is not null ? JsonSerializer.Serialize(variableSchema) : null,
            sampleVariables is not null ? JsonSerializer.Serialize(sampleVariables) : null,
            isSystem: true);

        dbContext.MessageTemplates.Add(template);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Registered template '{Name}' from module '{Module}'", name, moduleSource);
    }

    public async Task RegisterEventAsync(
        string eventName, string moduleSource, string displayName,
        string? description = null, CancellationToken ct = default)
    {
        var exists = await dbContext.EventRegistrations
            .IgnoreQueryFilters()
            .AnyAsync(e => e.EventName == eventName, ct);

        if (exists)
        {
            logger.LogDebug("Event '{EventName}' already registered, skipping", eventName);
            return;
        }

        var registration = EventRegistration.Create(eventName, moduleSource, displayName, description);
        dbContext.EventRegistrations.Add(registration);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Registered event '{EventName}' from module '{Module}'", eventName, moduleSource);
    }
}
