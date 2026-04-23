using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowHistory;

public sealed record GetWorkflowHistoryQuery(
    Guid InstanceId) : IRequest<Result<List<WorkflowStepRecord>>>;
