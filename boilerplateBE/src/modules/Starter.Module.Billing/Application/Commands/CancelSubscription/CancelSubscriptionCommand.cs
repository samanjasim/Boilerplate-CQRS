using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Commands.CancelSubscription;

public sealed record CancelSubscriptionCommand(Guid? TenantId) : IRequest<Result>;
