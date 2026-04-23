using Starter.Abstractions.Paging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Auth.Queries.GetInvitations;

internal sealed class GetInvitationsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetInvitationsQuery, Result<PaginatedList<InvitationDto>>>
{
    public async Task<Result<PaginatedList<InvitationDto>>> Handle(GetInvitationsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Invitations
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(i => i.Email.ToLower().Contains(searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var invitations = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Collect role and user IDs for lookup
        var roleIds = invitations.Select(i => i.RoleId).Distinct().ToList();
        var inviterIds = invitations.Select(i => i.InvitedBy).Distinct().ToList();

        var roles = await context.Roles
            .IgnoreQueryFilters()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var inviters = await context.Users
            .IgnoreQueryFilters()
            .Where(u => inviterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName.GetFullName(), cancellationToken);

        var dtos = invitations.Select(i => new InvitationDto(
            i.Id,
            i.Email,
            roles.GetValueOrDefault(i.RoleId, "Unknown"),
            inviters.GetValueOrDefault(i.InvitedBy, "Unknown"),
            i.ExpiresAt,
            i.IsAccepted,
            i.CreatedAt)).ToList();

        var paginatedList = PaginatedList<InvitationDto>.Create(
            dtos,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(paginatedList);
    }
}
