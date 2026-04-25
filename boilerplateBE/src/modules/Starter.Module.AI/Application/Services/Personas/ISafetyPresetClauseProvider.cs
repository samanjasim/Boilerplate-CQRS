using System.Globalization;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Personas;

internal interface ISafetyPresetClauseProvider
{
    string GetClause(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture);
}
