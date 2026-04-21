using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowDefinitions;

public sealed record GetWorkflowDefinitionsQuery(
    string? EntityType = null) : IRequest<Result<List<WorkflowDefinitionSummary>>>;
