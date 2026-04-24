using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Runtime;

internal sealed record PersonaContext(
    Guid Id,
    string Slug,
    PersonaAudienceType Audience,
    SafetyPreset Safety,
    IReadOnlyList<string> PermittedAgentSlugs);
