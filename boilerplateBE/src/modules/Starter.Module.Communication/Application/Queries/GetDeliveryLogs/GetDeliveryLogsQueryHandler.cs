using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Models;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryLogs;

internal sealed class GetDeliveryLogsQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetDeliveryLogsQuery, Result<PaginatedList<DeliveryLogDto>>>
{
    public async Task<Result<PaginatedList<DeliveryLogDto>>> Handle(
        GetDeliveryLogsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.DeliveryLogs
            .AsNoTracking()
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(d => d.Status == request.Status.Value);

        if (request.Channel.HasValue)
            query = query.Where(d => d.Channel == request.Channel.Value);

        if (!string.IsNullOrWhiteSpace(request.TemplateName))
            query = query.Where(d => d.TemplateName.Contains(request.TemplateName));

        if (request.From.HasValue)
            query = query.Where(d => d.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(d => d.CreatedAt <= request.To.Value);

        var projected = query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DeliveryLogDto(
                d.Id,
                d.RecipientUserId,
                d.RecipientAddress,
                d.TemplateName,
                d.Channel,
                d.IntegrationType,
                d.Provider,
                d.Subject,
                d.BodyPreview,
                d.Status,
                d.ProviderMessageId,
                d.ErrorMessage,
                d.TotalDurationMs,
                d.Attempts.Count,
                d.CreatedAt,
                d.ModifiedAt));

        var result = await PaginatedList<DeliveryLogDto>.CreateAsync(
            projected, request.PageNumber, request.PageSize, cancellationToken);

        return Result.Success(result);
    }
}
