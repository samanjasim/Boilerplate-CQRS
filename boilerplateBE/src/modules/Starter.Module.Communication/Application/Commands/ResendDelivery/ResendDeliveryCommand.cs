using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.ResendDelivery;

public sealed record ResendDeliveryCommand(Guid Id) : IRequest<Result<DeliveryLogDto>>;
