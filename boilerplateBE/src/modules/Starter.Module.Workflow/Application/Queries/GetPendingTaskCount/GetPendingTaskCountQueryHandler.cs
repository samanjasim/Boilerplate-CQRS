using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetPendingTaskCount;

internal sealed class GetPendingTaskCountQueryHandler(
    IWorkflowService workflowService,
    ICurrentUserService currentUser) : IRequestHandler<GetPendingTaskCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(
        GetPendingTaskCountQuery request, CancellationToken cancellationToken)
    {
        var count = await workflowService.GetPendingTaskCountAsync(
            currentUser.UserId!.Value, cancellationToken);

        return Result.Success(count);
    }
}
