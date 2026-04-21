using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowDefinitionDetail;

public sealed record GetWorkflowDefinitionDetailQuery(
    Guid DefinitionId) : IRequest<Result<WorkflowDefinitionDetail?>>;
