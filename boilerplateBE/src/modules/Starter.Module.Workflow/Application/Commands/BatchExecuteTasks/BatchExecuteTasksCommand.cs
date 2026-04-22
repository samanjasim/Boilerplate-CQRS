using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;

public sealed record BatchExecuteTasksCommand(
    IReadOnlyList<Guid> TaskIds,
    string Action,
    string? Comment = null) : IRequest<Result<BatchExecuteResult>>;
