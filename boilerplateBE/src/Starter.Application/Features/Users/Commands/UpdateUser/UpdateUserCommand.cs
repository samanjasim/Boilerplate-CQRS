using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber) : IRequest<Result>;
