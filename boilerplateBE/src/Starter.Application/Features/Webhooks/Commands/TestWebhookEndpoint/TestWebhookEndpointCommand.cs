using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Commands.TestWebhookEndpoint;

public sealed record TestWebhookEndpointCommand(Guid Id) : IRequest<Result<Unit>>;
