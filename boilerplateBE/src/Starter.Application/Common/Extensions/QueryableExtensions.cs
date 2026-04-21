using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;

namespace Starter.Application.Common.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Materializes an <see cref="IQueryable{T}"/> into a <see cref="PaginatedList{T}"/>
    /// by counting the total, skipping to the requested page, and taking the page size.
    /// </summary>
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<T>(items, totalCount, pageNumber, pageSize);
    }
}
