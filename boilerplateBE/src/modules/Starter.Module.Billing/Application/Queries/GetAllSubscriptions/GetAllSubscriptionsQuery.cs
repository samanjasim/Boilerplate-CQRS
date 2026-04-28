using System.ComponentModel;
using Starter.Abstractions.Paging;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Models;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetAllSubscriptions;

[AiTool(
    Name = "list_subscriptions",
    Description = "List active and past tenant subscriptions, including plan name, status, and renewal date. Read-only.",
    Category = "Billing",
    RequiredPermission = BillingPermissions.View,
    IsReadOnly = true)]
public sealed record GetAllSubscriptionsQuery(
    [Description("Page number, 1-indexed.")]
    int PageNumber = 1,

    [Description("Page size; max 100.")]
    int PageSize = 20,

    [Description("Optional free-text search across tenant or plan name.")]
    string? SearchTerm = null) : IRequest<Result<PaginatedList<SubscriptionSummaryDto>>>;
