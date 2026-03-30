using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Commands.SetGlobalDefaultRole;

public sealed record SetGlobalDefaultRoleCommand(
    Guid? RoleId) : IRequest<Result>;
