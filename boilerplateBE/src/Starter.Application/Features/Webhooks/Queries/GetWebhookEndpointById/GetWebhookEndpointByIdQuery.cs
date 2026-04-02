using MediatR;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookEndpointById;

public sealed record GetWebhookEndpointByIdQuery(Guid Id) : IRequest<Result<WebhookEndpointDto>>;
