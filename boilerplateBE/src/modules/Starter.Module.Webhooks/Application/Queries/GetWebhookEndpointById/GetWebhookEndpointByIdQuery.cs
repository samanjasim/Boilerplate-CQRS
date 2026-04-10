using MediatR;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookEndpointById;

public sealed record GetWebhookEndpointByIdQuery(Guid Id) : IRequest<Result<WebhookEndpointDto>>;
