using Starter.Application.Common.Models;
using Starter.Application.Features.Users.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Users.Queries.GetUsers;

public sealed record GetUsersQuery : PaginationQuery, IRequest<Result<PaginatedList<UserDto>>>
{
    public string? Status { get; init; }
    public string? Role { get; init; }
}
