using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.Safety.DeactivateSafetyPresetProfile;
using Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;
using Starter.Module.AI.Application.Queries.Safety.GetModerationEvents;
using Starter.Module.AI.Application.Queries.Safety.GetSafetyPresetProfiles;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/safety")]
public sealed class AiSafetyController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet("profiles")]
    [Authorize(Policy = AiPermissions.SafetyProfilesManage)]
    public async Task<IActionResult> GetProfiles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetSafetyPresetProfilesQuery(page, pageSize), ct);
        return HandlePagedResult(result);
    }

    [HttpPost("profiles")]
    [Authorize(Policy = AiPermissions.SafetyProfilesManage)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertSafetyPresetProfileCommand command,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("profiles/{profileId:guid}")]
    [Authorize(Policy = AiPermissions.SafetyProfilesManage)]
    public async Task<IActionResult> Deactivate(Guid profileId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeactivateSafetyPresetProfileCommand(profileId), ct);
        return HandleResult(result);
    }

    [HttpGet("moderation-events")]
    [Authorize(Policy = AiPermissions.ModerationView)]
    public async Task<IActionResult> GetModerationEvents(
        [FromQuery] GetModerationEventsQuery query,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(query, ct);
        return HandlePagedResult(result);
    }
}
