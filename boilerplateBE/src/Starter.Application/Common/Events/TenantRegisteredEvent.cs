namespace Starter.Application.Common.Events;

/// <summary>
/// Published when a new tenant is registered. Modules use this to provision
/// tenant-scoped resources (e.g. Billing creates a free-tier subscription).
/// </summary>
public sealed record TenantRegisteredEvent(
    Guid TenantId,
    string TenantName,
    string Slug,
    Guid OwnerUserId,
    DateTime OccurredAt
) : IDomainEvent;
