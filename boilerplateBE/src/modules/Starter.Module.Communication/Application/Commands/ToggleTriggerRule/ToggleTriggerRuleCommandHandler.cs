using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.ToggleTriggerRule;

internal sealed class ToggleTriggerRuleCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<ToggleTriggerRuleCommand, Result<TriggerRuleDto>>
{
    public async Task<Result<TriggerRuleDto>> Handle(
        ToggleTriggerRuleCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.TriggerRules
            .Include(r => r.IntegrationTargets)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result.Failure<TriggerRuleDto>(CommunicationErrors.TriggerRuleNotFound);

        if (entity.Status == TriggerRuleStatus.Active)
            entity.Deactivate();
        else
            entity.Activate();

        await dbContext.SaveChangesAsync(cancellationToken);

        // Resolve template name
        var templateName = await dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .Where(t => t.Id == entity.MessageTemplateId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(entity.ToDto(templateName));
    }
}
