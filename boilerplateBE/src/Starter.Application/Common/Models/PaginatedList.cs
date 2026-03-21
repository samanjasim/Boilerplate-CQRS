using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Common.Models;

public sealed class PaginatedList<T>
{
    public IReadOnlyCollection<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages { get; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    private PaginatedList(IReadOnlyCollection<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source,
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

    public static PaginatedList<T> Create(
        IReadOnlyCollection<T> items,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        return new PaginatedList<T>(items, totalCount, pageNumber, pageSize);
    }

    public PaginatedList<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        var mappedItems = Items.Select(mapper).ToList().AsReadOnly();
        return PaginatedList<TResult>.Create(mappedItems, TotalCount, PageNumber, PageSize);
    }
}
