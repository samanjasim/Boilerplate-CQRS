using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

public sealed record GetWorkflowAnalyticsQuery(
    Guid DefinitionId,
    WindowSelector Window) : IRequest<Result<WorkflowAnalyticsDto>>;
