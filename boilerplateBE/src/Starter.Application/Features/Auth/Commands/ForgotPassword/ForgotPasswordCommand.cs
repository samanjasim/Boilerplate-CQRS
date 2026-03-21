using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;
