using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPayments;

public sealed record GetPaymentsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? TenantId = null) : IRequest<Result<PaginatedList<PaymentRecordDto>>>;
