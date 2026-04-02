using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Commands.DeleteWebhookEndpoint;

public sealed record DeleteWebhookEndpointCommand(Guid Id) : IRequest<Result<Unit>>;
