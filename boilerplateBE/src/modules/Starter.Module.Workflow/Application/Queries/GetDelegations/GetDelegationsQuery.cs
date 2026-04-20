using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetDelegations;

public sealed record GetDelegationsQuery : IRequest<Result<List<DelegationRuleDto>>>;
