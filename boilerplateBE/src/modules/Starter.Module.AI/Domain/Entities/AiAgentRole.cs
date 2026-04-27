using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAgentRole : BaseEntity
{
    public Guid AgentPrincipalId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public Guid AssignedByUserId { get; private set; }

    private AiAgentRole() { }

    private AiAgentRole(Guid id, Guid principalId, Guid roleId, Guid assigner, DateTimeOffset assignedAt) : base(id)
    {
        AgentPrincipalId = principalId;
        RoleId = roleId;
        AssignedByUserId = assigner;
        AssignedAt = assignedAt;
    }

    public static AiAgentRole Create(Guid principalId, Guid roleId, Guid assigner) =>
        new(Guid.NewGuid(), principalId, roleId, assigner, DateTimeOffset.UtcNow);
}
