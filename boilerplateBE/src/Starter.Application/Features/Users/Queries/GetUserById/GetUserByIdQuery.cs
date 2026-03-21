using Starter.Application.Features.Users.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDto>>;
