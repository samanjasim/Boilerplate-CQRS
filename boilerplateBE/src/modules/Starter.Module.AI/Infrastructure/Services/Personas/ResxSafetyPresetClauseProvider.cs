using System.Globalization;
using System.Resources;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class ResxSafetyPresetClauseProvider : ISafetyPresetClauseProvider
{
    private static readonly ResourceManager Manager = new(
        "Starter.Module.AI.Resources.SafetyPresets",
        typeof(ResxSafetyPresetClauseProvider).Assembly);

    public string GetClause(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture)
    {
        var key = $"{preset}.{audience}";
        var localised = Manager.GetString(key, culture);
        if (!string.IsNullOrEmpty(localised))
            return localised;

        var fallback = Manager.GetString(key, CultureInfo.InvariantCulture);
        return fallback ?? string.Empty;
    }
}
