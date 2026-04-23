using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowInstanceById;

public sealed record GetWorkflowInstanceByIdQuery(Guid InstanceId)
    : IRequest<Result<WorkflowInstanceSummary>>;
