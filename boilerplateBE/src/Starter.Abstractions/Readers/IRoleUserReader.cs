namespace Starter.Abstractions.Readers;

/// <summary>
/// Resolves user IDs for a given role. Used by workflow assignee resolution
/// to find "any user with Admin role" without coupling to identity entities.
/// </summary>
public interface IRoleUserReader
{
    Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(
        string roleName,
        Guid? tenantId = null,
        CancellationToken ct = default);
}
