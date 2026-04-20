using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Web;
using Starter.Module.Workflow.Application.Commands.CancelDelegation;
using Starter.Module.Workflow.Application.Commands.CancelWorkflow;
using Starter.Module.Workflow.Application.Commands.CloneDefinition;
using Starter.Module.Workflow.Application.Commands.CreateDelegation;
using Starter.Module.Workflow.Application.Commands.ExecuteTask;
using Starter.Module.Workflow.Application.Commands.StartWorkflow;
using Starter.Module.Workflow.Application.Commands.TransitionWorkflow;
using Starter.Module.Workflow.Application.Commands.UpdateDefinition;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Module.Workflow.Application.Queries.GetActiveDelegation;
using Starter.Module.Workflow.Application.Queries.GetDelegations;
using Starter.Module.Workflow.Application.Queries.GetPendingTaskCount;
using Starter.Module.Workflow.Application.Queries.GetPendingTasks;
using Starter.Module.Workflow.Application.Queries.GetWorkflowDefinitionDetail;
using Starter.Module.Workflow.Application.Queries.GetWorkflowDefinitions;
using Starter.Module.Workflow.Application.Queries.GetWorkflowHistory;
using Starter.Module.Workflow.Application.Queries.GetWorkflowInstances;
using Starter.Module.Workflow.Application.Queries.GetWorkflowStatus;
using Starter.Module.Workflow.Constants;
using Starter.Shared.Models;

namespace Starter.Module.Workflow.Controllers;

[RequireFeatureFlag("workflow.enabled")]
public sealed class WorkflowController(ISender mediator) : BaseApiController(mediator)
{
    // ── Definitions ─────────────────────────────────────────────────────────

    [HttpGet("definitions")]
    [Authorize(Policy = WorkflowPermissions.View)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkflowDefinitionSummary>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDefinitions(
        [FromQuery] string? entityType, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWorkflowDefinitionsQuery(entityType), ct);
        return HandleResult(result);
    }

    [HttpGet("definitions/{id:guid}")]
    [Authorize(Policy = WorkflowPermissions.View)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowDefinitionDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefinition(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWorkflowDefinitionDetailQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost("definitions/{id:guid}/clone")]
    [Authorize(Policy = WorkflowPermissions.ManageDefinitions)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloneDefinition(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new CloneDefinitionCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPut("definitions/{id:guid}")]
    [Authorize(Policy = WorkflowPermissions.ManageDefinitions)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDefinition(
        Guid id, [FromBody] UpdateDefinitionCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command with { DefinitionId = id }, ct);
        return HandleResult(result);
    }

    // ── Instances ────────────────────────────────────────────────────────────

    [HttpPost("instances")]
    [Authorize(Policy = WorkflowPermissions.Start)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartWorkflow(
        [FromBody] StartWorkflowCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpGet("instances")]
    [Authorize(Policy = WorkflowPermissions.View)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkflowInstanceSummary>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstances(
        [FromQuery] string? entityType,
        [FromQuery] string? state,
        [FromQuery] Guid? startedByUserId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetWorkflowInstancesQuery(entityType, state, startedByUserId, status, page, pageSize), ct);
        return HandleResult(result);
    }

    [HttpGet("instances/status")]
    [Authorize(Policy = WorkflowPermissions.View)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowStatusSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWorkflowStatusQuery(entityType, entityId), ct);
        return HandleResult(result);
    }

    [HttpGet("instances/{instanceId:guid}/history")]
    [Authorize(Policy = WorkflowPermissions.View)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkflowStepRecord>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(Guid instanceId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWorkflowHistoryQuery(instanceId), ct);
        return HandleResult(result);
    }

    [HttpPost("instances/{instanceId:guid}/cancel")]
    [Authorize(Policy = WorkflowPermissions.Cancel)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelWorkflow(
        Guid instanceId, [FromBody] CancelWorkflowRequest? request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new CancelWorkflowCommand(instanceId, request?.Reason), ct);
        return HandleResult(result);
    }

    [HttpPost("instances/{instanceId:guid}/transition")]
    [Authorize(Policy = WorkflowPermissions.Start)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TransitionWorkflow(
        Guid instanceId, [FromBody] TransitionWorkflowRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new TransitionWorkflowCommand(instanceId, request.Trigger), ct);
        return HandleResult(result);
    }

    // ── Tasks ────────────────────────────────────────────────────────────────

    [HttpGet("tasks")]
    [Authorize(Policy = WorkflowPermissions.ActOnTask)]
    [ProducesResponseType(typeof(ApiResponse<List<PendingTaskSummary>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingTasks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPendingTasksQuery(page, pageSize), ct);
        return HandleResult(result);
    }

    [HttpGet("tasks/count")]
    [Authorize(Policy = WorkflowPermissions.ActOnTask)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingTaskCount(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPendingTaskCountQuery(), ct);
        return HandleResult(result);
    }

    [HttpPost("tasks/{taskId:guid}/execute")]
    [Authorize(Policy = WorkflowPermissions.ActOnTask)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteTask(
        Guid taskId, [FromBody] ExecuteTaskRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new ExecuteTaskCommand(taskId, request.Action, request.Comment), ct);
        return HandleResult(result);
    }

    // ── Delegations ─────────────────────────────────────────────────────────

    [HttpPost("delegations")]
    [Authorize(Policy = WorkflowPermissions.ActOnTask)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDelegation(
        [FromBody] CreateDelegationRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new CreateDelegationCommand(request.ToUserId, request.StartDate, request.EndDate), ct);
        return HandleResult(result);
    }

    [HttpGet("delegations")]
    [Authorize(Policy = WorkflowPermissions.ActOnTask)]
    [ProducesResponseType(typeof(ApiResponse<List<DelegationRuleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDelegations(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDelegationsQuery(), ct);
        return HandleResult(result);
    }

    [HttpDelete("delegations/{id:guid}")]
    [Authorize(Policy = WorkflowPermissions.ActOnTask)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelDelegation(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new CancelDelegationCommand(id), ct);
        return HandleResult(result);
    }

    [HttpGet("delegations/active")]
    [Authorize(Policy = WorkflowPermissions.ActOnTask)]
    [ProducesResponseType(typeof(ApiResponse<DelegationRuleDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveDelegation(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetActiveDelegationQuery(), ct);
        return HandleResult(result);
    }
}

public sealed record CancelWorkflowRequest(string? Reason);

public sealed record ExecuteTaskRequest(string Action, string? Comment);

public sealed record TransitionWorkflowRequest(string Trigger);

public sealed record CreateDelegationRequest(Guid ToUserId, DateTime StartDate, DateTime EndDate);
