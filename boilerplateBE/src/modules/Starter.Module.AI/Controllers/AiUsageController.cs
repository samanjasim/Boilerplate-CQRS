using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Queries.GetTenantUsage;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/usage")]
public sealed class AiUsageController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet("me")]
    [Authorize(Policy = AiPermissions.ViewUsage)]
    public async Task<IActionResult> GetTenantUsage(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetTenantUsageQuery(), ct);
        return HandleResult(result);
    }
}
