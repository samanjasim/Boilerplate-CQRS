using MediatR;
using Starter.Abstractions.Paging;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Safety.GetSafetyPresetProfiles;

public sealed record GetSafetyPresetProfilesQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PaginatedList<SafetyPresetProfileDto>>>;
