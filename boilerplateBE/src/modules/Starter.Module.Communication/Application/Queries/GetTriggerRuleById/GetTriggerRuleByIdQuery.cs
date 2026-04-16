using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetTriggerRuleById;

public sealed record GetTriggerRuleByIdQuery(Guid Id) : IRequest<Result<TriggerRuleDto>>;
