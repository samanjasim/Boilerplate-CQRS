using Starter.Abstractions.Ai;
using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Events;

/// <summary>
/// Raised whenever an `AiPersona` is updated. Consumed by downstream caches that resolve
/// persona-derived safety thresholds (e.g., the moderation pipeline) so they can invalidate
/// any cached entries for this `(TenantId, PersonaId)` pair.
/// </summary>
public sealed record PersonaUpdatedEvent(
    Guid? TenantId,
    Guid PersonaId,
    string Slug,
    SafetyPreset SafetyPreset) : DomainEventBase;
