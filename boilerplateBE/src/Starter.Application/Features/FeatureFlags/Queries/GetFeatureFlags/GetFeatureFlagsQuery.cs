using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.FeatureFlags.DTOs;
using Starter.Domain.FeatureFlags.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.FeatureFlags.Queries.GetFeatureFlags;

public sealed record GetFeatureFlagsQuery(
    int PageNumber = 1, int PageSize = 50,
    FlagCategory? Category = null, string? Search = null) : IRequest<Result<PaginatedList<FeatureFlagDto>>>;
