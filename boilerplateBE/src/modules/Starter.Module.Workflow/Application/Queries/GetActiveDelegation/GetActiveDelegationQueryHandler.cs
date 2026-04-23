using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetActiveDelegation;

internal sealed class GetActiveDelegationQueryHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IUserReader userReader) : IRequestHandler<GetActiveDelegationQuery, Result<DelegationRuleDto?>>
{
    public async Task<Result<DelegationRuleDto?>> Handle(
        GetActiveDelegationQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;
        var now = DateTime.UtcNow;

        var rule = await dbContext.DelegationRules
            .FirstOrDefaultAsync(r => r.FromUserId == userId
                && r.IsActive
                && r.StartDate <= now
                && r.EndDate >= now, cancellationToken);

        if (rule is null)
            return Result.Success<DelegationRuleDto?>(null);

        string? toDisplayName = null;
        var user = await userReader.GetAsync(rule.ToUserId, cancellationToken);
        if (user is not null)
            toDisplayName = user.DisplayName;

        return Result.Success<DelegationRuleDto?>(new DelegationRuleDto(
            rule.Id,
            rule.ToUserId,
            toDisplayName,
            rule.StartDate,
            rule.EndDate,
            rule.IsActive));
    }
}
