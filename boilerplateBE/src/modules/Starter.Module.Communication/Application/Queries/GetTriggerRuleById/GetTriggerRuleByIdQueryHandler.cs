using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetTriggerRuleById;

internal sealed class GetTriggerRuleByIdQueryHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<GetTriggerRuleByIdQuery, Result<TriggerRuleDto>>
{
    public async Task<Result<TriggerRuleDto>> Handle(
        GetTriggerRuleByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.TriggerRules
            .AsNoTracking()
            .Include(r => r.IntegrationTargets)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result.Failure<TriggerRuleDto>(CommunicationErrors.TriggerRuleNotFound);

        var templateName = await dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .Where(t => t.Id == entity.MessageTemplateId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(entity.ToDto(templateName));
    }
}
