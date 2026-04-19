using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.StartWorkflow;

public sealed record StartWorkflowCommand(
    string EntityType,
    Guid EntityId,
    string DefinitionName,
    Dictionary<string, object>? Context = null) : IRequest<Result<Guid>>;
