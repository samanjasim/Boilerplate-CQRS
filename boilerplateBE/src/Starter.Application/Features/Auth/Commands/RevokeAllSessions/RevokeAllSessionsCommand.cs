using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.RevokeAllSessions;

public sealed record RevokeAllSessionsCommand(
    string? CurrentRefreshToken = null) : IRequest<Result>;
