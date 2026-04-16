using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.Communication.Application.Queries.GetCommunicationDashboard;
using Starter.Module.Communication.Constants;

namespace Starter.Module.Communication.Controllers;

/// <summary>
/// Communication dashboard statistics.
/// </summary>
public sealed class CommunicationDashboardController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator)
{
    /// <summary>
    /// Get communication dashboard statistics for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = CommunicationPermissions.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetCommunicationDashboardQuery(), ct);
        return HandleResult(result);
    }
}
