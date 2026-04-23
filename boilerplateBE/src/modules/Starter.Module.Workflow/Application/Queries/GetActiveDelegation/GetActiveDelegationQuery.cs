using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetActiveDelegation;

public sealed record GetActiveDelegationQuery : IRequest<Result<DelegationRuleDto?>>;
