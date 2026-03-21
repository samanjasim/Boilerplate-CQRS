namespace Starter.Domain.Identity.Entities;

public sealed class RolePermission
{
    public Guid RoleId { get; private set; }
    public Role? Role { get; private set; }

    public Guid PermissionId { get; private set; }
    public Permission? Permission { get; private set; }

    public DateTime AssignedAt { get; private set; }

    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        AssignedAt = DateTime.UtcNow;
    }
}
