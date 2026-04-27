using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAgentRoles;

public sealed record GetAgentRolesQuery(Guid AssistantId) : IRequest<Result<IReadOnlyList<AgentRoleDto>>>;

public sealed record AgentRoleDto(Guid RoleId, string RoleName, DateTimeOffset AssignedAt);
