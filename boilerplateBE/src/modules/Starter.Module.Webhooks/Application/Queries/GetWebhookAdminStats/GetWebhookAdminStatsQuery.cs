using MediatR;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookAdminStats;

public sealed record GetWebhookAdminStatsQuery : IRequest<Result<WebhookAdminStatsDto>>;
