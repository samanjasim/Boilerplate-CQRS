namespace Starter.Abstractions.Paging;

/// <summary>
/// Unified paged result used across the solution. Lives in Starter.Abstractions
/// so capability contracts, application handlers, and controllers can share one
/// shape. EF-specific paging (<c>IQueryable.ToPaginatedListAsync</c>) lives as
/// an extension in Starter.Application to keep this type framework-free.
/// </summary>
public sealed class PaginatedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PaginatedList(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public static PaginatedList<T> Create(
        IReadOnlyCollection<T> items,
        int totalCount,
        int pageNumber,
        int pageSize) =>
        new((items as IReadOnlyList<T>) ?? items.ToList(), totalCount, pageNumber, pageSize);

    public PaginatedList<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        var mapped = Items.Select(mapper).ToList();
        return new PaginatedList<TResult>(mapped, TotalCount, PageNumber, PageSize);
    }
}
