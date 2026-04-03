using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Domain.Webhooks.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookDeliveriesAdmin;

public sealed record GetWebhookDeliveriesAdminQuery(
    Guid EndpointId,
    int PageNumber = 1,
    int PageSize = 20,
    WebhookDeliveryStatus? Status = null) : IRequest<Result<PaginatedList<WebhookDeliveryDto>>>;
