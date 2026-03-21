using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Email, string Code) : IRequest<Result>;
