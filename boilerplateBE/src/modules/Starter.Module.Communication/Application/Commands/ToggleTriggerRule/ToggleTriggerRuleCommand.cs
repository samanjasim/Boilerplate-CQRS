using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.ToggleTriggerRule;

public sealed record ToggleTriggerRuleCommand(Guid Id) : IRequest<Result<TriggerRuleDto>>;
