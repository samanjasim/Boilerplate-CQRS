using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateTriggerRule;

internal sealed class UpdateTriggerRuleCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<UpdateTriggerRuleCommand, Result<TriggerRuleDto>>
{
    public async Task<Result<TriggerRuleDto>> Handle(
        UpdateTriggerRuleCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.TriggerRules
            .Include(r => r.IntegrationTargets)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result.Failure<TriggerRuleDto>(CommunicationErrors.TriggerRuleNotFound);

        // Verify template exists
        var template = await dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.MessageTemplateId, cancellationToken);

        if (template is null)
            return Result.Failure<TriggerRuleDto>(CommunicationErrors.TemplateNotFound);

        var channelSequenceJson = JsonSerializer.Serialize(request.ChannelSequence);

        entity.Update(
            request.Name,
            request.EventName,
            request.MessageTemplateId,
            request.RecipientMode,
            channelSequenceJson,
            request.DelaySeconds,
            request.ConditionJson);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.ToDto(template.Name));
    }
}
