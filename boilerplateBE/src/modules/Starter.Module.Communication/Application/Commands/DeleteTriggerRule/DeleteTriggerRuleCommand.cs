using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteTriggerRule;

public sealed record DeleteTriggerRuleCommand(Guid Id) : IRequest<Result>;
