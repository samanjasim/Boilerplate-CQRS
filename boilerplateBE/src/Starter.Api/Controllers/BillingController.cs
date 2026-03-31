using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Billing.Commands.CancelSubscription;
using Starter.Application.Features.Billing.Commands.ChangePlan;
using Starter.Application.Features.Billing.Commands.CreatePlan;
using Starter.Application.Features.Billing.Commands.DeactivatePlan;
using Starter.Application.Features.Billing.Commands.ResyncPlanTenants;
using Starter.Application.Features.Billing.Commands.UpdatePlan;
using Starter.Application.Features.Billing.DTOs;
using Starter.Application.Features.Billing.Queries.GetAllSubscriptions;
using Starter.Application.Features.Billing.Queries.GetPayments;
using Starter.Application.Features.Billing.Queries.GetPlanById;
using Starter.Application.Features.Billing.Queries.GetPlanOptions;
using Starter.Application.Features.Billing.Queries.GetPlans;
using Starter.Application.Features.Billing.Queries.GetSubscription;
using Starter.Application.Features.Billing.Queries.GetUsage;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Constants;
using Starter.Shared.Models;

namespace Starter.Api.Controllers;

public sealed class BillingController(ISender mediator) : BaseApiController(mediator)
{
    // ─── Public ───────────────────────────────────────────────────────────────

    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<SubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicPlans(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlansQuery(PublicOnly: true), ct);
        return HandleResult(result);
    }

    // ─── Tenant ───────────────────────────────────────────────────────────────

    [HttpGet("subscription")]
    [Authorize(Policy = Permissions.Billing.View)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetSubscriptionQuery(), ct);
        return HandleResult(result);
    }

    [HttpPost("change-plan")]
    [Authorize(Policy = Permissions.Billing.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ChangePlanCommand(request.PlanId, request.Interval, null), ct);
        return HandleResult(result);
    }

    [HttpPost("cancel")]
    [Authorize(Policy = Permissions.Billing.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new CancelSubscriptionCommand(null), ct);
        return HandleResult(result);
    }

    [HttpGet("payments")]
    [Authorize(Policy = Permissions.Billing.View)]
    [ProducesResponseType(typeof(PagedApiResponse<PaymentRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPaymentsQuery(pageNumber, pageSize), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("usage")]
    [Authorize(Policy = Permissions.Billing.View)]
    [ProducesResponseType(typeof(ApiResponse<UsageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsage(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetUsageQuery(), ct);
        return HandleResult(result);
    }

    // ─── SuperAdmin: Plan Management ─────────────────────────────────────────

    [HttpGet("plan-options")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    [ProducesResponseType(typeof(ApiResponse<List<PlanOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanOptions(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlanOptionsQuery(), ct);
        return HandleResult(result);
    }

    [HttpGet("plans/manage")]
    [Authorize(Policy = Permissions.Billing.ViewPlans)]
    [ProducesResponseType(typeof(ApiResponse<List<SubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPlans(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlansQuery(IncludeInactive: includeInactive), ct);
        return HandleResult(result);
    }

    [HttpGet("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ViewPlans)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlanByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost("plans/create")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleCreatedResult(result, nameof(GetPlanById), new { id = result.IsSuccess ? result.Value : (Guid?)null });
    }

    [HttpPut("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdatePlanCommand command, CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeactivatePlanCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("plans/{id:guid}/resync")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResyncPlanTenants(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ResyncPlanTenantsCommand(id), ct);
        return HandleResult(result);
    }

    // ─── SuperAdmin: Tenant Subscription Management ──────────────────────────

    /// <summary>
    /// Get all tenant subscriptions with usage and payment summary (SuperAdmin).
    /// </summary>
    [HttpGet("subscriptions")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    [ProducesResponseType(typeof(PagedApiResponse<SubscriptionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSubscriptions(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAllSubscriptionsQuery(pageNumber, pageSize, searchTerm), ct);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get usage for a specific tenant (SuperAdmin).
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}/usage")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    [ProducesResponseType(typeof(ApiResponse<UsageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantUsage(Guid tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetUsageQuery(tenantId), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get payment history for a specific tenant (SuperAdmin).
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}/payments")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    [ProducesResponseType(typeof(PagedApiResponse<PaymentRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantPayments(
        Guid tenantId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPaymentsQuery(pageNumber, pageSize, tenantId), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("tenants/{tenantId:guid}/subscription")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantSubscription(Guid tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetSubscriptionQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPost("tenants/{tenantId:guid}/change-plan")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeTenantPlan(
        Guid tenantId, [FromBody] ChangePlanRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ChangePlanCommand(request.PlanId, request.Interval, tenantId), ct);
        return HandleResult(result);
    }
}

public sealed record ChangePlanRequest(Guid PlanId, BillingInterval? Interval = null);
