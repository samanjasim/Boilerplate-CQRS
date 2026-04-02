using MediatR;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookEndpoints;

public sealed record GetWebhookEndpointsQuery : IRequest<Result<List<WebhookEndpointDto>>>;
