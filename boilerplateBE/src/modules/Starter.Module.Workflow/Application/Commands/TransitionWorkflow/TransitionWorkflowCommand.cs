using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.TransitionWorkflow;

public sealed record TransitionWorkflowCommand(
    Guid InstanceId,
    string Trigger) : IRequest<Result<bool>>;
