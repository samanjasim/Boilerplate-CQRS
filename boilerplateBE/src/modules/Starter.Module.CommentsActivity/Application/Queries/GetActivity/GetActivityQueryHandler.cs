using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Models;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Module.CommentsActivity.Domain.Entities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Application.Queries.GetActivity;

internal sealed class GetActivityQueryHandler(
    CommentsActivityDbContext context,
    IUserReader userReader) : IRequestHandler<GetActivityQuery, Result<PaginatedList<ActivityEntryDto>>>
{
    public async Task<Result<PaginatedList<ActivityEntryDto>>> Handle(
        GetActivityQuery request, CancellationToken cancellationToken)
    {
        var query = context.ActivityEntries
            .AsNoTracking()
            .Where(a => a.EntityType == request.EntityType && a.EntityId == request.EntityId)
            .OrderBy(a => a.CreatedAt);

        var page = await PaginatedList<ActivityEntry>.CreateAsync(
            query, request.PageNumber, request.PageSize, cancellationToken);

        var actorIds = page.Items
            .Where(a => a.ActorId.HasValue)
            .Select(a => a.ActorId!.Value)
            .Distinct();

        var users = await userReader.GetManyAsync(actorIds, cancellationToken);
        var userMap = users.ToDictionary(u => u.Id);

        var result = page.Map(a => new ActivityEntryDto(
            a.Id,
            a.EntityType,
            a.EntityId,
            a.Action,
            a.ActorId,
            a.ActorId.HasValue && userMap.TryGetValue(a.ActorId.Value, out var user)
                ? user.DisplayName
                : null,
            a.MetadataJson,
            a.Description,
            a.CreatedAt));

        return Result.Success(result);
    }
}
