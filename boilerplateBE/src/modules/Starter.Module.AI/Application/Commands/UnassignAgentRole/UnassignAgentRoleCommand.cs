using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UnassignAgentRole;

public sealed record UnassignAgentRoleCommand(Guid AssistantId, Guid RoleId) : IRequest<Result>;
