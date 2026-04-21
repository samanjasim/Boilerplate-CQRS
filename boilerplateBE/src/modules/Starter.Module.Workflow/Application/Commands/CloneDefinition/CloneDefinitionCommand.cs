using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CloneDefinition;

public sealed record CloneDefinitionCommand(Guid DefinitionId) : IRequest<Result<Guid>>;
