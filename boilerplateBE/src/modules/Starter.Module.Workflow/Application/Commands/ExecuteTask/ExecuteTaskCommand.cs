using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.ExecuteTask;

public sealed record ExecuteTaskCommand(
    Guid TaskId,
    string Action,
    string? Comment = null) : IRequest<Result<bool>>;
