using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Readers;

public sealed class FileReader(IApplicationDbContext db) : IFileReader
{
    public async Task<FileSummary?> GetAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        return await db.Set<FileMetadata>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(f => f.Id == fileId)
            .Select(f => new FileSummary(f.Id, f.FileName, f.ContentType, f.Size))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FileSummary>> GetManyAsync(
        IEnumerable<Guid> fileIds,
        CancellationToken cancellationToken = default)
    {
        var ids = fileIds.ToList();
        if (ids.Count == 0) return [];

        return await db.Set<FileMetadata>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new FileSummary(f.Id, f.FileName, f.ContentType, f.Size))
            .ToListAsync(cancellationToken);
    }
}
