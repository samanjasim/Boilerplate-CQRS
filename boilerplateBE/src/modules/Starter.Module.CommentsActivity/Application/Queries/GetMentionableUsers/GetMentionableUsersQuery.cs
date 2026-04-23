using MediatR;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetMentionableUsers;

public sealed record GetMentionableUsersQuery(
    string? Search,
    int PageSize = 10,
    string? EntityType = null,
    Guid? EntityId = null) : IRequest<Result<List<MentionableUserDto>>>;
