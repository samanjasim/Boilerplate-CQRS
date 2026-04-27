using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.AssignAgentRole;

public sealed record AssignAgentRoleCommand(Guid AssistantId, Guid RoleId) : IRequest<Result>;
