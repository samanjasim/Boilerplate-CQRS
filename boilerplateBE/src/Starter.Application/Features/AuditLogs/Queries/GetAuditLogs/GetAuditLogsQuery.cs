using System.ComponentModel;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Domain.Common.Enums;
using Starter.Shared.Constants;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogs;

[AiTool(
    Name = "list_audit_logs",
    Description = "List audit-log entries for the current tenant. Supports filtering by entity type, entity id, action, performing user, and date range. Read-only.",
    Category = "Audit",
    RequiredPermission = Starter.Shared.Constants.Permissions.System.ViewAuditLogs,
    IsReadOnly = true)]
public sealed record GetAuditLogsQuery(
    [Description("Filter by audit entity type, e.g. 'User', 'Role', 'AiAssistant'.")]
    AuditEntityType? EntityType = null,

    [Description("Filter by the id of the audited entity.")]
    Guid? EntityId = null,

    [Description("Filter by audit action, e.g. 'Create', 'Update', 'Delete'.")]
    AuditAction? Action = null,

    [Description("Filter by the user id that performed the action.")]
    Guid? PerformedBy = null,

    [Description("Earliest occurred-at timestamp (UTC) to include.")]
    DateTime? DateFrom = null,

    [Description("Latest occurred-at timestamp (UTC) to include.")]
    DateTime? DateTo = null) : PaginationQuery, IRequest<Result<PaginatedList<AuditLogDto>>>;
