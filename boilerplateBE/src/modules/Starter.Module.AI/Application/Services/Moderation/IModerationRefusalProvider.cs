using System.Globalization;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

internal interface IModerationRefusalProvider
{
    string GetRefusal(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture);
    string GetProviderUnavailable(SafetyPreset preset, CultureInfo culture);
}
