using Starter.Domain.Common;

namespace Starter.Domain.Identity.Entities;

public sealed class Role : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? TenantId { get; private set; }

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    private readonly List<RolePermission> _rolePermissions = [];
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    private Role() { }

    private Role(Guid id, string name, string? description, bool isSystemRole, Guid? tenantId = null)
        : base(id)
    {
        Name = name;
        Description = description;
        IsSystemRole = isSystemRole;
        IsActive = true;
        TenantId = tenantId;
    }

    public static Role Create(string name, string? description = null, bool isSystemRole = false, Guid? tenantId = null)
    {
        return new Role(Guid.NewGuid(), name, description, isSystemRole, tenantId);
    }

    public void Update(string name, string? description)
    {
        // Defense-in-depth: handler validates first via Result pattern
        if (IsSystemRole)
            throw new InvalidOperationException("Cannot modify a system role.");

        Name = name;
        Description = description;
    }

    public void AddPermission(Permission permission)
    {
        if (_rolePermissions.Any(rp => rp.PermissionId == permission.Id))
            return;

        _rolePermissions.Add(new RolePermission(Id, permission.Id));
    }

    public void RemovePermission(Guid permissionId)
    {
        var rolePermission = _rolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (rolePermission is not null)
            _rolePermissions.Remove(rolePermission);
    }

    public void ClearPermissions()
    {
        _rolePermissions.Clear();
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        // Defense-in-depth: handler validates first via Result pattern
        if (IsSystemRole)
            throw new InvalidOperationException("Cannot deactivate a system role.");

        IsActive = false;
    }

    public bool HasPermission(string permissionName)
    {
        return _rolePermissions.Any(rp =>
            rp.Permission?.Name.Equals(permissionName, StringComparison.OrdinalIgnoreCase) == true);
    }
}
