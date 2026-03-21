using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<Result>;
