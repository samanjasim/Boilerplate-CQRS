using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Dashboard.Queries.GetDashboardAnalytics;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

public sealed class DashboardController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet("analytics")]
    [Authorize(Policy = Permissions.System.ViewDashboard)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] string period = "30d", CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDashboardAnalyticsQuery(period), ct);
        return HandleResult(result);
    }
}
