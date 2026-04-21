using Starter.Abstractions.Paging;
using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Queries.GetLoginHistory;

internal sealed class GetLoginHistoryQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetLoginHistoryQuery, Result<PaginatedList<LoginHistoryDto>>>
{
    public async Task<Result<PaginatedList<LoginHistoryDto>>> Handle(GetLoginHistoryQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<PaginatedList<LoginHistoryDto>>(UserErrors.Unauthorized());

        var query = context.LoginHistory
            .AsNoTracking()
            .Where(lh => lh.UserId == userId.Value)
            .OrderByDescending(lh => lh.CreatedAt)
            .Select(lh => new LoginHistoryDto(
                lh.Id,
                lh.Email,
                lh.IpAddress,
                lh.DeviceInfo,
                lh.Success,
                lh.FailureReason,
                lh.CreatedAt));

        var paginatedList = await query.ToPaginatedListAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(paginatedList);
    }
}
