using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Billing.Commands.CancelSubscription;
using Starter.Application.Features.Billing.Commands.ChangePlan;
using Starter.Application.Features.Billing.Commands.CreatePlan;
using Starter.Application.Features.Billing.Commands.DeactivatePlan;
using Starter.Application.Features.Billing.Commands.ResyncPlanTenants;
using Starter.Application.Features.Billing.Commands.UpdatePlan;
using Starter.Application.Features.Billing.Queries.GetPayments;
using Starter.Application.Features.Billing.Queries.GetPlanById;
using Starter.Application.Features.Billing.Queries.GetPlans;
using Starter.Application.Features.Billing.Queries.GetSubscription;
using Starter.Application.Features.Billing.Queries.GetUsage;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

public sealed class BillingController(ISender mediator) : BaseApiController(mediator)
{
    // ─── Public ───────────────────────────────────────────────────────────────

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicPlans(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlansQuery(PublicOnly: true), ct);
        return HandleResult(result);
    }

    // ─── Tenant ───────────────────────────────────────────────────────────────

    [HttpGet("subscription")]
    [Authorize(Policy = Permissions.Billing.View)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetSubscriptionQuery(), ct);
        return HandleResult(result);
    }

    [HttpPost("change-plan")]
    [Authorize(Policy = Permissions.Billing.Manage)]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ChangePlanCommand(request.PlanId, request.Interval, null), ct);
        return HandleResult(result);
    }

    [HttpPost("cancel")]
    [Authorize(Policy = Permissions.Billing.Manage)]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new CancelSubscriptionCommand(null), ct);
        return HandleResult(result);
    }

    [HttpGet("payments")]
    [Authorize(Policy = Permissions.Billing.View)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPaymentsQuery(pageNumber, pageSize), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("usage")]
    [Authorize(Policy = Permissions.Billing.View)]
    public async Task<IActionResult> GetUsage(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetUsageQuery(), ct);
        return HandleResult(result);
    }

    // ─── SuperAdmin: Plan Management ─────────────────────────────────────────

    [HttpGet("plans/manage")]
    [Authorize(Policy = Permissions.Billing.ViewPlans)]
    public async Task<IActionResult> GetAllPlans(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlansQuery(IncludeInactive: includeInactive), ct);
        return HandleResult(result);
    }

    [HttpGet("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ViewPlans)]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPlanByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost("plans/create")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdatePlanCommand command, CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("plans/{id:guid}")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeactivatePlanCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("plans/{id:guid}/resync")]
    [Authorize(Policy = Permissions.Billing.ManagePlans)]
    public async Task<IActionResult> ResyncPlanTenants(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ResyncPlanTenantsCommand(id), ct);
        return HandleResult(result);
    }

    // ─── SuperAdmin: Tenant Subscription Management ──────────────────────────

    [HttpGet("tenants/{tenantId:guid}/subscription")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    public async Task<IActionResult> GetTenantSubscription(Guid tenantId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetSubscriptionQuery(tenantId), ct);
        return HandleResult(result);
    }

    [HttpPost("tenants/{tenantId:guid}/change-plan")]
    [Authorize(Policy = Permissions.Billing.ManageTenantSubscriptions)]
    public async Task<IActionResult> ChangeTenantPlan(
        Guid tenantId, [FromBody] ChangePlanRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ChangePlanCommand(request.PlanId, request.Interval, tenantId), ct);
        return HandleResult(result);
    }
}

public sealed record ChangePlanRequest(Guid PlanId, BillingInterval? Interval = null);
