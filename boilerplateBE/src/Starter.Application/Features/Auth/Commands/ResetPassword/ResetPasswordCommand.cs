using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string Code,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<Result>;
