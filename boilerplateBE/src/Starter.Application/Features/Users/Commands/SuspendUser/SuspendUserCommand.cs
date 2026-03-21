using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Commands.SuspendUser;

public sealed record SuspendUserCommand(Guid Id) : IRequest<Result>;
