using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.Users.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Users.Queries.GetUsers;

[AiTool(
    Name = "list_users",
    Description = "List users in the current tenant, paged and optionally filtered by status or role.",
    Category = "Users",
    RequiredPermission = Starter.Shared.Constants.Permissions.Users.View,
    IsReadOnly = true)]
public sealed record GetUsersQuery : PaginationQuery, IRequest<Result<PaginatedList<UserDto>>>
{
    [Description("Filter by user status, e.g. 'Active', 'Suspended'.")]
    public string? Status { get; init; }

    [Description("Filter by role name, e.g. 'Admin', 'User'.")]
    public string? Role { get; init; }
}
