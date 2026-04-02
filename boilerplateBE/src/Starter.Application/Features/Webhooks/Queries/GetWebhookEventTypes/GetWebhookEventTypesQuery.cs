using MediatR;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookEventTypes;

public sealed record GetWebhookEventTypesQuery : IRequest<Result<List<WebhookEventTypeDto>>>;
