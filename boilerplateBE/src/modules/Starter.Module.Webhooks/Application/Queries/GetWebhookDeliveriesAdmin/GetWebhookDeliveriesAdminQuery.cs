using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.Webhooks.Domain.Enums;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookDeliveriesAdmin;

public sealed record GetWebhookDeliveriesAdminQuery(
    Guid EndpointId,
    int PageNumber = 1,
    int PageSize = 20,
    WebhookDeliveryStatus? Status = null) : IRequest<Result<PaginatedList<WebhookDeliveryDto>>>;
