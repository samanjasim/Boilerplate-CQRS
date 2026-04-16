using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetTriggerRules;

public sealed record GetTriggerRulesQuery : IRequest<Result<List<TriggerRuleDto>>>;
