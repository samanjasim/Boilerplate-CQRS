namespace Starter.Application.Common.Interfaces;

public interface IPermissionHierarchyService
{
    Task<bool> CanAssignRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> ArePermissionsWithinCeilingAsync(IEnumerable<Guid> permissionIds, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetCurrentUserPermissionNamesAsync(CancellationToken cancellationToken = default);
}
