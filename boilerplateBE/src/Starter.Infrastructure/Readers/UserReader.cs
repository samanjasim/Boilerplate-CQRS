using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Readers;

public sealed class UserReader(IApplicationDbContext db) : IUserReader
{
    public async Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => new UserSummary(
                u.Id,
                u.TenantId,
                u.Username,
                u.Email.Value,
                u.FullName.FirstName + " " + u.FullName.LastName,
                u.Status.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSummary>> GetManyAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0) return [];

        return await db.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserSummary(
                u.Id,
                u.TenantId,
                u.Username,
                u.Email.Value,
                u.FullName.FirstName + " " + u.FullName.LastName,
                u.Status.Name))
            .ToListAsync(cancellationToken);
    }
}
