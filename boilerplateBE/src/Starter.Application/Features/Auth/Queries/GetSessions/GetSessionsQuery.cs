using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Queries.GetSessions;

public sealed record GetSessionsQuery(
    string? CurrentRefreshToken = null) : IRequest<Result<List<SessionDto>>>;
