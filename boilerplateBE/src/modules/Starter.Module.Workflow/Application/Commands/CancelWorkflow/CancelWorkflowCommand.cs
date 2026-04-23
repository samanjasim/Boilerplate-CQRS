using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CancelWorkflow;

public sealed record CancelWorkflowCommand(
    Guid InstanceId,
    string? Reason = null) : IRequest<Result>;
