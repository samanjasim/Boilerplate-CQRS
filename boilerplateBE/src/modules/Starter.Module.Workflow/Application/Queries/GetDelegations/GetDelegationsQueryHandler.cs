using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetDelegations;

internal sealed class GetDelegationsQueryHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IUserReader userReader) : IRequestHandler<GetDelegationsQuery, Result<List<DelegationRuleDto>>>
{
    public async Task<Result<List<DelegationRuleDto>>> Handle(
        GetDelegationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var rules = await dbContext.DelegationRules
            .Where(r => r.FromUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        // Resolve display names for delegates
        var delegateIds = rules.Select(r => r.ToUserId).Distinct().ToList();
        var nameLookup = new Dictionary<Guid, string>();
        if (delegateIds.Count > 0)
        {
            var users = await userReader.GetManyAsync(delegateIds, cancellationToken);
            foreach (var u in users)
                nameLookup[u.Id] = u.DisplayName;
        }

        var dtos = rules.Select(r => new DelegationRuleDto(
            r.Id,
            r.ToUserId,
            nameLookup.TryGetValue(r.ToUserId, out var name) ? name : null,
            r.StartDate,
            r.EndDate,
            r.IsActive)).ToList();

        return Result.Success(dtos);
    }
}
