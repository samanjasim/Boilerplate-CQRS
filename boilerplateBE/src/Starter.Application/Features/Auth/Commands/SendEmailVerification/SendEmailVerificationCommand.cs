using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.SendEmailVerification;

public sealed record SendEmailVerificationCommand(string Email) : IRequest<Result>;
