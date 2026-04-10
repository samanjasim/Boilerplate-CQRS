namespace Starter.Abstractions.Readers;

/// <summary>
/// Read-only access to tenant data from outside the core <c>ApplicationDbContext</c>.
///
/// Modules with their own DbContext cannot join across contexts, and cross-module
/// EF navigation is forbidden by design. When a module needs tenant info (name,
/// slug, status), it injects this reader instead of <c>ApplicationDbContext</c>.
///
/// Returns plain DTOs — no entity tracking, no navigation properties. The cost
/// is one extra query per call, which is fine for the 99% case. If a module's
/// hot path needs a denormalized snapshot, it should subscribe to tenant
/// lifecycle events and maintain its own copy.
/// </summary>
public interface ITenantReader
{
    Task<TenantSummary?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantSummary>> GetManyAsync(
        IEnumerable<Guid> tenantIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Plain projection of a tenant. Status is exposed as a string so the contract
/// does not leak the <c>TenantStatus</c> value object from the domain layer.
/// </summary>
public sealed record TenantSummary(
    Guid Id,
    string Name,
    string? Slug,
    string Status);
