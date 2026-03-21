using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.AcceptInvite;

public sealed record AcceptInviteCommand(
    string Token,
    string FirstName,
    string LastName,
    string Password,
    string ConfirmPassword) : IRequest<Result>;
