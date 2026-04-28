using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.AuditLogs.Queries.GetAuditLogById;
using Starter.Application.Features.AuditLogs.Queries.GetAuditLogs;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Audit log endpoints.
/// </summary>
[Authorize(Policy = Permissions.System.ViewAuditLogs)]
public sealed class AuditLogsController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get paginated audit logs with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs([FromQuery] GetAuditLogsQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get a single audit log entry by id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLog(Guid id)
    {
        var result = await Mediator.Send(new GetAuditLogByIdQuery(id));
        return HandleResult(result);
    }
}
