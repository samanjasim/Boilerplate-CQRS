using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPayments;

internal sealed class GetPaymentsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetPaymentsQuery, Result<PaginatedList<PaymentRecordDto>>>
{
    public async Task<Result<PaginatedList<PaymentRecordDto>>> Handle(
        GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;

        var query = context.PaymentRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(p => p.TenantId == tenantId.Value);

        query = query.OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(p => p.ToDto()).ToList();
        var paginatedList = PaginatedList<PaymentRecordDto>.Create(
            dtos.AsReadOnly(), totalCount, request.PageNumber, request.PageSize);

        return Result.Success(paginatedList);
    }
}
