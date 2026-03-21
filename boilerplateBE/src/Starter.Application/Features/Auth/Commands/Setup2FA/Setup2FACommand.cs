using Starter.Application.Features.Auth.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.Setup2FA;

public sealed record Setup2FACommand() : IRequest<Result<Setup2FAResponseDto>>;
