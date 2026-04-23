using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.CreateTriggerRule;

public sealed record CreateTriggerRuleCommand(
    string Name,
    string EventName,
    Guid MessageTemplateId,
    string RecipientMode,
    string[] ChannelSequence,
    int DelaySeconds,
    string? ConditionJson) : IRequest<Result<TriggerRuleDto>>;
