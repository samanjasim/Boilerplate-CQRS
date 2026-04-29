using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Domain.Common;
using Starter.Shared.Results;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogById;

internal sealed class GetAuditLogByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetAuditLogByIdQuery, Result<AuditLogDto>>
{
    public async Task<Result<AuditLogDto>> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(a => a.Id == request.Id)
            .Select(a => new AuditLogDto(
                a.Id,
                a.EntityType.ToString(),
                a.EntityId,
                a.Action.ToString(),
                a.Changes,
                a.PerformedBy,
                a.PerformedByName,
                a.PerformedAt,
                a.IpAddress,
                a.CorrelationId,
                a.OnBehalfOfUserId,
                a.AgentPrincipalId,
                a.AgentRunId))
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
            return Result.Failure<AuditLogDto>(Error.NotFound("AuditLog.NotFound", "Audit log not found"));

        return Result.Success(dto);
    }
}
