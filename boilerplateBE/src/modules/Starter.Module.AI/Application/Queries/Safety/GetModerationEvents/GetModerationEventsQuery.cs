using MediatR;
using Starter.Abstractions.Paging;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Safety.GetModerationEvents;

public sealed record GetModerationEventsQuery(
    DateTime? From = null,
    DateTime? To = null,
    ModerationOutcome? Outcome = null,
    ModerationStage? Stage = null,
    Guid? AssistantId = null,
    int Page = 1,
    int PageSize = 50)
    : IRequest<Result<PaginatedList<ModerationEventDto>>>;
