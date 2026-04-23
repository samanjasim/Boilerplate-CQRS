using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.UpdateDefinition;

public sealed record UpdateDefinitionCommand(
    Guid DefinitionId,
    string? DisplayName,
    string? Description,
    string? StatesJson,
    string? TransitionsJson) : IRequest<Result>;
