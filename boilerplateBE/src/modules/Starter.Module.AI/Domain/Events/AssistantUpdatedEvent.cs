using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Events;

/// <summary>
/// Raised whenever an `AiAssistant` is mutated in a way that affects cost-cap resolution
/// (e.g., per-agent budget change). Consumed by the cost-cap resolver's cache to invalidate
/// the cached caps for this `(TenantId, AssistantId)` pair.
/// </summary>
public sealed record AssistantUpdatedEvent(Guid TenantId, Guid AssistantId) : DomainEventBase;
