using Starter.Application.Features.Users.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<Result<UserDto>>;
