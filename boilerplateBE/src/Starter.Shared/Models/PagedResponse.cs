namespace Starter.Shared.Models;

public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResponse<T> Create(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        return new PagedResponse<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public PagedResponse<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new PagedResponse<TResult>
        {
            Items = Items.Select(mapper).ToList(),
            PageNumber = PageNumber,
            PageSize = PageSize,
            TotalCount = TotalCount
        };
    }
}

public class PagedApiResponse<T> : ApiResponse
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public PaginationMetadata Pagination { get; init; } = new();

    public static PagedApiResponse<T> Ok(PagedResponse<T> pagedResponse) =>
        new()
        {
            Success = true,
            Data = pagedResponse.Items,
            Pagination = new PaginationMetadata
            {
                PageNumber = pagedResponse.PageNumber,
                PageSize = pagedResponse.PageSize,
                TotalCount = pagedResponse.TotalCount,
                TotalPages = pagedResponse.TotalPages,
                HasPreviousPage = pagedResponse.HasPreviousPage,
                HasNextPage = pagedResponse.HasNextPage
            }
        };

    public new static PagedApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message };
}

public class PaginationMetadata
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
}
