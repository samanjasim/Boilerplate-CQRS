using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid Id) : IRequest<Result>;
