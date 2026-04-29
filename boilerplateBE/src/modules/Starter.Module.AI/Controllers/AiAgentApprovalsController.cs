using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.Approvals.ApprovePendingAction;
using Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;
using Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovalById;
using Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovals;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/agents/approvals")]
public sealed class AiAgentApprovalsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.AgentsViewApprovals)]
    public async Task<IActionResult> List(
        [FromQuery] GetPendingApprovalsQuery query,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(query, ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.AgentsViewApprovals)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPendingApprovalByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AiPermissions.AgentsApproveAction)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveBody? body,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ApprovePendingActionCommand(id, body?.Note), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/deny")]
    [Authorize(Policy = AiPermissions.AgentsApproveAction)]
    public async Task<IActionResult> Deny(
        Guid id,
        [FromBody] DenyBody body,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DenyPendingActionCommand(id, body.Reason), ct);
        return HandleResult(result);
    }

    public sealed record ApproveBody(string? Note);
    public sealed record DenyBody(string Reason);
}
