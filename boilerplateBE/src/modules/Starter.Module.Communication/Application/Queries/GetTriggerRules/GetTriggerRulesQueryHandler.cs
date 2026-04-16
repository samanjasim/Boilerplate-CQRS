using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetTriggerRules;

internal sealed class GetTriggerRulesQueryHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<GetTriggerRulesQuery, Result<List<TriggerRuleDto>>>
{
    public async Task<Result<List<TriggerRuleDto>>> Handle(
        GetTriggerRulesQuery request,
        CancellationToken cancellationToken)
    {
        var rules = await dbContext.TriggerRules
            .AsNoTracking()
            .Include(r => r.IntegrationTargets)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        // Resolve template names
        var templateIds = rules.Select(r => r.MessageTemplateId).Distinct().ToList();
        var templateNames = await dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => templateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var dtos = rules.Select(r =>
            r.ToDto(templateNames.GetValueOrDefault(r.MessageTemplateId))).ToList();

        return Result.Success(dtos);
    }
}
