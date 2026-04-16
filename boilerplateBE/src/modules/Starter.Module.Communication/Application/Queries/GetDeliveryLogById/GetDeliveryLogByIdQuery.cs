using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryLogById;

public sealed record GetDeliveryLogByIdQuery(Guid Id) : IRequest<Result<DeliveryLogDetailDto>>;
