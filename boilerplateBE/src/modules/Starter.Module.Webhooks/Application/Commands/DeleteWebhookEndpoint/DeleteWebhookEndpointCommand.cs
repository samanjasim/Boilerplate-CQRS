using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.DeleteWebhookEndpoint;

public sealed record DeleteWebhookEndpointCommand(Guid Id) : IRequest<Result<Unit>>;
