using MediatR;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookEventTypes;

public sealed record GetWebhookEventTypesQuery : IRequest<Result<List<WebhookEventTypeDto>>>;
