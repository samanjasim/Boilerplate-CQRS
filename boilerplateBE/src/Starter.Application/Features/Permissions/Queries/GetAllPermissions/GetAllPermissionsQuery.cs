using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Permissions.Queries.GetAllPermissions;

public sealed record GetAllPermissionsQuery : IRequest<Result<IReadOnlyList<PermissionGroupDto>>>;
