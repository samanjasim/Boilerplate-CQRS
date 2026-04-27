using System.Globalization;
using System.Resources;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

internal sealed class ResxModerationRefusalProvider : IModerationRefusalProvider
{
    private static readonly ResourceManager Manager = new(
        "Starter.Module.AI.Resources.ModerationRefusalTemplates",
        typeof(ResxModerationRefusalProvider).Assembly);

    public string GetRefusal(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture)
    {
        var key = $"{preset}.{audience}";
        return Lookup(key, culture);
    }

    public string GetProviderUnavailable(SafetyPreset preset, CultureInfo culture)
    {
        var key = $"{preset}.ProviderUnavailable";
        return Lookup(key, culture);
    }

    private static string Lookup(string key, CultureInfo culture)
    {
        var localised = Manager.GetString(key, culture);
        if (!string.IsNullOrEmpty(localised)) return localised;
        var fallback = Manager.GetString(key, CultureInfo.InvariantCulture);
        return fallback ?? "Request not allowed.";
    }
}
