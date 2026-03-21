using Starter.Domain.Common;

namespace Starter.Domain.Identity.Entities;

public sealed class Permission : BaseEntity
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Module { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<RolePermission> _rolePermissions = [];
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    private Permission() { }

    private Permission(Guid id, string name, string? description, string? module)
        : base(id)
    {
        Name = name;
        Description = description;
        Module = module;
        IsActive = true;
    }

    public static Permission Create(string name, string? description = null, string? module = null)
    {
        return new Permission(Guid.NewGuid(), name, description, module);
    }

    public void Update(string name, string? description, string? module)
    {
        Name = name;
        Description = description;
        Module = module;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
