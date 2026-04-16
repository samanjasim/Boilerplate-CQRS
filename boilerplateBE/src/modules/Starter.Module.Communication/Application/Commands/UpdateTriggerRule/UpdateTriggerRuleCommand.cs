using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateTriggerRule;

public sealed record UpdateTriggerRuleCommand(
    Guid Id,
    string Name,
    string EventName,
    Guid MessageTemplateId,
    string RecipientMode,
    string[] ChannelSequence,
    int DelaySeconds,
    string? ConditionJson) : IRequest<Result<TriggerRuleDto>>;
