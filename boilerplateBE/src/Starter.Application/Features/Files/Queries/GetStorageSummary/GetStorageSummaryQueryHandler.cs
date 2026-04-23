using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Files.Queries.GetStorageSummary;

internal sealed class GetStorageSummaryQueryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetStorageSummaryQuery, Result<StorageSummaryDto>>
{
    public async Task<Result<StorageSummaryDto>> Handle(GetStorageSummaryQuery request, CancellationToken ct)
    {
        var query = db.Set<FileMetadata>().AsNoTracking()
            .Where(f => f.Status == FileStatus.Permanent);

        var crossTenant = request.AllTenants && currentUser.TenantId is null;
        if (!crossTenant)
            query = query.Where(f => f.TenantId == currentUser.TenantId);

        var byCategory = await query
            .GroupBy(f => f.Category)
            .Select(g => new CategoryBytes(g.Key.ToString(), g.Sum(f => f.Size), g.Count()))
            .ToListAsync(ct);

        var byEntityType = await query
            .GroupBy(f => f.EntityType)
            .Select(g => new EntityTypeBytes(g.Key, g.Sum(f => f.Size), g.Count()))
            .ToListAsync(ct);

        var top = await query
            .GroupBy(f => f.UploadedBy)
            .Select(g => new { UserId = g.Key, Bytes = g.Sum(f => f.Size), Count = g.Count() })
            .OrderByDescending(x => x.Bytes)
            .Take(10)
            .ToListAsync(ct);

        var topUserIds = top.Select(t => t.UserId).ToList();
        var userNames = await db.Users
            .Where(u => topUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName.FirstName, u.FullName.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        var topUploaders = top
            .Select(x => new UploaderBytes(x.UserId, userNames.GetValueOrDefault(x.UserId), x.Bytes, x.Count))
            .ToList();

        var total = byCategory.Sum(c => c.Bytes);
        return Result.Success(new StorageSummaryDto(total, byCategory, byEntityType, topUploaders));
    }
}
