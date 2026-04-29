using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Constants;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetSubscription;

[AiTool(
    Name = "get_my_subscription",
    Description = "Get the calling tenant's current subscription, including plan, status, billing interval, and current-period dates. Read-only and scoped to the caller's tenant.",
    Category = "Billing",
    RequiredPermission = BillingPermissions.View,
    IsReadOnly = true)]
public sealed record GetSubscriptionQuery(
    // TenantId is server-trusted: the handler resolves the caller's tenant from
    // ICurrentUserService when this is null. Hidden from the LLM tool schema so
    // an agent cannot ask about another tenant's subscription.
    [property: AiParameterIgnore]
    Guid? TenantId = null) : IRequest<Result<TenantSubscriptionDto>>;
