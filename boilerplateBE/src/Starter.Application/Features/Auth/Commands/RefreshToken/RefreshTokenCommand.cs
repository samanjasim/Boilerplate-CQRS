using Starter.Application.Features.Auth.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string RefreshToken) : IRequest<Result<LoginResponseDto>>;
