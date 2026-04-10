namespace Starter.Abstractions.Readers;

/// <summary>
/// Read-only access to role data from outside the core <c>ApplicationDbContext</c>.
/// See <see cref="ITenantReader"/> for the rationale.
/// </summary>
public interface IRoleReader
{
    Task<RoleSummary?> GetAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleSummary>> GetManyAsync(
        IEnumerable<Guid> roleIds,
        CancellationToken cancellationToken = default);

    Task<RoleSummary?> GetByNameAsync(
        string name,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}

public sealed record RoleSummary(
    Guid Id,
    string Name,
    Guid? TenantId,
    bool IsSystemRole);
