using MediatR;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookEndpoints;

public sealed record GetWebhookEndpointsQuery : IRequest<Result<List<WebhookEndpointDto>>>;
