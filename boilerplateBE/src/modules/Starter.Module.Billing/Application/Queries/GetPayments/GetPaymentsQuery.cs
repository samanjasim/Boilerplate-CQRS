using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetPayments;

public sealed record GetPaymentsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? TenantId = null) : IRequest<Result<PaginatedList<PaymentRecordDto>>>;
