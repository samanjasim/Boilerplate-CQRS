using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.Register;

public sealed record RegisterUserCommand(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string ConfirmPassword) : IRequest<Result<Guid>>;
