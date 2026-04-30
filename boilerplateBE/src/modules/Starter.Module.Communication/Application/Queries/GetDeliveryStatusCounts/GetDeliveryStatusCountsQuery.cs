using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;

public sealed record GetDeliveryStatusCountsQuery(int WindowDays = 7)
    : IRequest<Result<DeliveryStatusCountsDto>>;
