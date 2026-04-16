using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryLogById;

internal sealed class GetDeliveryLogByIdQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetDeliveryLogByIdQuery, Result<DeliveryLogDetailDto>>
{
    public async Task<Result<DeliveryLogDetailDto>> Handle(
        GetDeliveryLogByIdQuery request,
        CancellationToken cancellationToken)
    {
        var deliveryLog = await context.DeliveryLogs
            .AsNoTracking()
            .Include(d => d.Attempts)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (deliveryLog is null)
            return Result.Failure<DeliveryLogDetailDto>(
                new Error("DeliveryLog.NotFound", "Delivery log not found.", ErrorType.NotFound));

        return Result.Success(deliveryLog.ToDetailDto());
    }
}
