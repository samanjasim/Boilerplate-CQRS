using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="ITemplateRegistrar"/> registered when
/// the Communication module is not installed. Registration calls are silent
/// no-ops so modules can seed templates unconditionally.
/// </summary>
public sealed class NullTemplateRegistrar(ILogger<NullTemplateRegistrar> logger) : ITemplateRegistrar
{
    public Task RegisterTemplateAsync(
        string name, string moduleSource, string category, string? description,
        string? subjectTemplate, string bodyTemplate,
        NotificationChannelType defaultChannel, string[] availableChannels,
        Dictionary<string, string>? variableSchema = null,
        Dictionary<string, object>? sampleVariables = null,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Template registration skipped — Communication module not installed (template: {Name})",
            name);
        return Task.CompletedTask;
    }

    public Task RegisterEventAsync(
        string eventName, string moduleSource, string displayName,
        string? description = null, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Event registration skipped — Communication module not installed (event: {EventName})",
            eventName);
        return Task.CompletedTask;
    }
}
