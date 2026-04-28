using System.ComponentModel;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetUsage;

[AiTool(
    Name = "list_usage",
    Description = "Report the current tenant's usage records (requests, storage, AI tokens) for the current billing period. Read-only.",
    Category = "Billing",
    RequiredPermission = BillingPermissions.View,
    IsReadOnly = true)]
public sealed record GetUsageQuery(
    [Description("Optional tenant id; superadmin-only when set to a value other than the caller's tenant.")]
    Guid? TenantId = null) : IRequest<Result<UsageDto>>;
