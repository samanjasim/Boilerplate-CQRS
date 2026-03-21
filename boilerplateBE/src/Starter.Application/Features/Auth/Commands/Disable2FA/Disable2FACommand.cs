using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.Disable2FA;

public sealed record Disable2FACommand(string Code) : IRequest<Result>;
