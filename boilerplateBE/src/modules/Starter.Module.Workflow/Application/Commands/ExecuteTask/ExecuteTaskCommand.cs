using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.ExecuteTask;

public sealed record ExecuteTaskCommand(
    Guid TaskId,
    string Action,
    string? Comment = null,
    Dictionary<string, object>? FormData = null) : IRequest<Result<bool>>;
