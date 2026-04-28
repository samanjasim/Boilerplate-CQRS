using MediatR;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogById;

public sealed record GetAuditLogByIdQuery(Guid Id) : IRequest<Result<AuditLogDto>>;
