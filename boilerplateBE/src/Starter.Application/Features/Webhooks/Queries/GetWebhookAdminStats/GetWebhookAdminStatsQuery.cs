using MediatR;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookAdminStats;

public sealed record GetWebhookAdminStatsQuery : IRequest<Result<WebhookAdminStatsDto>>;
