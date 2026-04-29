using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Events;

/// <summary>
/// Raised whenever an `AiSafetyPresetProfile` is created, updated, or deactivated.
/// Consumed by downstream caches (e.g., the safety-preset profile resolver) to invalidate
/// the cached threshold profile for this `(TenantId, Preset, Provider)` triple.
/// `TenantId == null` denotes a platform-default row affecting every tenant that does not
/// have its own override.
/// </summary>
public sealed record SafetyPresetProfileUpdatedEvent(
    Guid? TenantId,
    Guid ProfileId) : DomainEventBase;
