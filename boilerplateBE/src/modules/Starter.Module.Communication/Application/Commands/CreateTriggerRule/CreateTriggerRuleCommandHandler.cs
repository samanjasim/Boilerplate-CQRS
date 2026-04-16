using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.CreateTriggerRule;

internal sealed class CreateTriggerRuleCommandHandler(
    CommunicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateTriggerRuleCommand, Result<TriggerRuleDto>>
{
    public async Task<Result<TriggerRuleDto>> Handle(
        CreateTriggerRuleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<TriggerRuleDto>(CommunicationErrors.TenantRequired);

        // Verify template exists
        var template = await dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.MessageTemplateId, cancellationToken);

        if (template is null)
            return Result.Failure<TriggerRuleDto>(CommunicationErrors.TemplateNotFound);

        var channelSequenceJson = JsonSerializer.Serialize(request.ChannelSequence);

        var entity = TriggerRule.Create(
            tenantId.Value,
            request.Name,
            request.EventName,
            request.MessageTemplateId,
            request.RecipientMode,
            channelSequenceJson,
            request.DelaySeconds,
            request.ConditionJson);

        dbContext.TriggerRules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.ToDto(template.Name));
    }
}
