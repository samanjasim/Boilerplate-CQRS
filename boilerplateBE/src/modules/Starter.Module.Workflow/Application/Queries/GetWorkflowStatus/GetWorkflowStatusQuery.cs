using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowStatus;

public sealed record GetWorkflowStatusQuery(
    string EntityType,
    Guid EntityId) : IRequest<Result<WorkflowStatusSummary?>>;
