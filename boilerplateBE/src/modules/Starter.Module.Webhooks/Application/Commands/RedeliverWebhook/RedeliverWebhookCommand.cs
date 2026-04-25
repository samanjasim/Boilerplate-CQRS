using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.RedeliverWebhook;

public sealed record RedeliverWebhookCommand(Guid DeliveryId) : IRequest<Result<Unit>>;
