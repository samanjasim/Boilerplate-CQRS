using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiRoleMetadata : BaseEntity
{
    public Guid RoleId { get; private set; }
    public bool IsAgentAssignable { get; private set; }

    private AiRoleMetadata() { }
    private AiRoleMetadata(Guid id, Guid roleId, bool isAgentAssignable) : base(id)
    {
        RoleId = roleId;
        IsAgentAssignable = isAgentAssignable;
    }

    public static AiRoleMetadata Create(Guid roleId, bool isAgentAssignable) =>
        new(Guid.NewGuid(), roleId, isAgentAssignable);

    public void SetAgentAssignable(bool value) => IsAgentAssignable = value;
}
