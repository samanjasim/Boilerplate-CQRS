using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Commands.UnlockUser;

public sealed record UnlockUserCommand(Guid Id) : IRequest<Result>;
