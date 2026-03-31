using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Commands.CancelSubscription;

public sealed record CancelSubscriptionCommand(Guid? TenantId) : IRequest<Result>;
