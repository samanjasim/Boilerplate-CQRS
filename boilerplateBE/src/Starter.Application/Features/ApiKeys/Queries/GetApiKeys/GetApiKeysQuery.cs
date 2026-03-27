using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeys;

public sealed record GetApiKeysQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? KeyType = null,
    Guid? TenantId = null) : IRequest<Result<PaginatedList<ApiKeyDto>>>;
