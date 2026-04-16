using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetComments;

public sealed record GetCommentsQuery(
    string EntityType,
    Guid EntityId,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PaginatedList<CommentDto>>>;
