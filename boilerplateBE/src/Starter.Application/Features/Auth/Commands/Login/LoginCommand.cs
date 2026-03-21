using Starter.Application.Features.Auth.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? TwoFactorCode = null) : IRequest<Result<LoginResponseDto>>;
