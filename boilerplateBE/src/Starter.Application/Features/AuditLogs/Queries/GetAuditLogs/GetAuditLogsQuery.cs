using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogs;

public sealed record GetAuditLogsQuery(
    AuditEntityType? EntityType = null,
    Guid? EntityId = null,
    AuditAction? Action = null,
    Guid? PerformedBy = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null) : PaginationQuery, IRequest<Result<PaginatedList<AuditLogDto>>>;
