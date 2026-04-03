using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetAllWebhookEndpoints;

public sealed record GetAllWebhookEndpointsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<WebhookAdminSummaryDto>>>;
