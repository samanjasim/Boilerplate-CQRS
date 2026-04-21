using Starter.Abstractions.Paging;
using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetAllWebhookEndpoints;

public sealed record GetAllWebhookEndpointsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<WebhookAdminSummaryDto>>>;
