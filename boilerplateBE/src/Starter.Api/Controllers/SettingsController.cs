using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Settings.Commands.UpdateSetting;
using Starter.Application.Features.Settings.Commands.UpdateSettings;
using Starter.Application.Features.Settings.Queries.GetSettings;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// System settings endpoints.
/// </summary>
[Authorize(Policy = Permissions.System.ManageSettings)]
public sealed class SettingsController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get all settings grouped by category.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSettings()
    {
        var result = await Mediator.Send(new GetSettingsQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Batch update multiple settings.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Update a single setting by key.
    /// </summary>
    [HttpPut("{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingValueRequest request)
    {
        var result = await Mediator.Send(new UpdateSettingCommand(key, request.Value));
        return HandleResult(result);
    }
}

/// <summary>
/// Request body for updating a single setting value.
/// </summary>
public sealed record UpdateSettingValueRequest(string Value);
