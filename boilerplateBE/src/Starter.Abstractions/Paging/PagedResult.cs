namespace Starter.Abstractions.Paging;

/// <summary>
/// Pure-contract paged result. Lives in Starter.Abstractions so capability
/// interfaces can expose paginated reads without depending on Starter.Application
/// or Starter.Shared. Controllers/handlers convert to the wire-level
/// <c>PagedApiResponse&lt;T&gt;</c> in Starter.Shared.Models.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
