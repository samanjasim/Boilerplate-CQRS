using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid Id) : IRequest<Result>;
