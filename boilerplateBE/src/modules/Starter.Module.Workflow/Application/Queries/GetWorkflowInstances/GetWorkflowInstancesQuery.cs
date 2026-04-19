using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowInstances;

public sealed record GetWorkflowInstancesQuery(
    string? EntityType = null,
    string? State = null,
    Guid? StartedByUserId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<List<WorkflowInstanceSummary>>>;
