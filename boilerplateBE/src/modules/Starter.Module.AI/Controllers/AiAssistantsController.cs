using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Features.Access.Commands.GrantResourceAccess;
using Starter.Application.Features.Access.Commands.RevokeResourceAccess;
using Starter.Application.Features.Access.Commands.SetResourceVisibility;
using Starter.Application.Features.Access.Commands.TransferResourceOwnership;
using Starter.Application.Features.Access.Queries.ListResourceGrants;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Commands.AssignAgentRole;
using Starter.Module.AI.Application.Commands.CreateAssistant;
using Starter.Module.AI.Application.Commands.DeleteAssistant;
using Starter.Module.AI.Application.Commands.SetAgentBudget;
using Starter.Module.AI.Application.Commands.SetAssistantAccessMode;
using Starter.Module.AI.Application.Commands.UnassignAgentRole;
using Starter.Module.AI.Application.Commands.UpdateAssistant;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetAgentBudget;
using Starter.Module.AI.Application.Queries.GetAgentRoles;
using Starter.Module.AI.Application.Queries.GetAgentUsage;
using Starter.Module.AI.Application.Queries.GetAssistantById;
using Starter.Module.AI.Application.Queries.GetAssistants;
using Starter.Module.AI.Application.Queries.GetTenantUsage;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/assistants")]
public sealed class AiAssistantsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(PagedApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetAssistantsQuery(pageNumber, pageSize, searchTerm, isActive), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAssistantByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssistantCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAssistantCommand command,
        CancellationToken ct = default)
    {
        if (ValidateRouteId(id, command.Id) is { } mismatch) return mismatch;
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteAssistantCommand(id), ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}/grants")]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListGrants(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ListResourceGrantsQuery(ResourceTypes.AiAssistant, id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/grants")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GrantAccess(
        Guid id,
        [FromBody] GrantAssistantAccessRequest request,
        CancellationToken ct = default)
    {
        var command = new GrantResourceAccessCommand(
            ResourceTypes.AiAssistant,
            id,
            request.SubjectType,
            request.SubjectId,
            request.Level);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}/grants/{grantId:guid}")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeGrant(Guid id, Guid grantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new RevokeResourceAccessCommand(grantId), ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}/visibility")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetVisibility(
        Guid id,
        [FromBody] SetAssistantVisibilityRequest request,
        CancellationToken ct = default)
    {
        var command = new SetResourceVisibilityCommand(ResourceTypes.AiAssistant, id, request.Visibility);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}/access-mode")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAccessMode(
        Guid id,
        [FromBody] SetAssistantAccessModeRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new SetAssistantAccessModeCommand(id, request.AccessMode), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/transfer-ownership")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TransferOwnership(
        Guid id,
        [FromBody] TransferAssistantOwnershipRequest request,
        CancellationToken ct = default)
    {
        var command = new TransferResourceOwnershipCommand(
            ResourceTypes.AiAssistant, id, request.NewOwnerId);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    // ----- Plan 5d-1 endpoints: per-agent budget, roles, usage -----

    [HttpPut("{id:guid}/budget")]
    [Authorize(Policy = AiPermissions.ManageAgentBudget)]
    public async Task<IActionResult> SetBudget(
        Guid id,
        [FromBody] SetAgentBudgetRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new SetAgentBudgetCommand(
            id, request.MonthlyCostCapUsd, request.DailyCostCapUsd, request.RequestsPerMinute), ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}/budget")]
    [Authorize(Policy = AiPermissions.ViewUsage)]
    public async Task<IActionResult> GetBudget(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAgentBudgetQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/roles")]
    [Authorize(Policy = AiPermissions.AssignAgentRole)]
    public async Task<IActionResult> AssignRole(
        Guid id,
        [FromBody] AssignAgentRoleRequest request,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new AssignAgentRoleCommand(id, request.RoleId), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = AiPermissions.AssignAgentRole)]
    public async Task<IActionResult> UnassignRole(Guid id, Guid roleId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UnassignAgentRoleCommand(id, roleId), ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}/roles")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    public async Task<IActionResult> ListRoles(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAgentRolesQuery(id), ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}/usage")]
    [Authorize(Policy = AiPermissions.ViewUsage)]
    public async Task<IActionResult> GetUsage(
        Guid id,
        [FromQuery] string window = "monthly",
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAgentUsageQuery(id, window), ct);
        return HandleResult(result);
    }
}

public sealed record GrantAssistantAccessRequest(
    GrantSubjectType SubjectType,
    Guid SubjectId,
    AccessLevel Level);

public sealed record SetAssistantVisibilityRequest(ResourceVisibility Visibility);

public sealed record SetAssistantAccessModeRequest(AssistantAccessMode AccessMode);

public sealed record TransferAssistantOwnershipRequest(Guid NewOwnerId);

public sealed record SetAgentBudgetRequest(
    decimal? MonthlyCostCapUsd,
    decimal? DailyCostCapUsd,
    int? RequestsPerMinute);

public sealed record AssignAgentRoleRequest(Guid RoleId);
