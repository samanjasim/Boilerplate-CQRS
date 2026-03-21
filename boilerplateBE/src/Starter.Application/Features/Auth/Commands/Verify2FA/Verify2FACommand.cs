using Starter.Application.Features.Auth.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.Verify2FA;

public sealed record Verify2FACommand(string Secret, string Code) : IRequest<Result<Verify2FAResponseDto>>;
