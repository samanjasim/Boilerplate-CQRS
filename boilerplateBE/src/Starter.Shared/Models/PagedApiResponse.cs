using Starter.Abstractions.Paging;

namespace Starter.Shared.Models;

public class PagedApiResponse<T> : ApiResponse
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public PaginationMetadata Pagination { get; init; } = new();

    public static PagedApiResponse<T> Ok(PaginatedList<T> paged) =>
        new()
        {
            Success = true,
            Data = paged.Items,
            Pagination = new PaginationMetadata
            {
                PageNumber = paged.PageNumber,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount,
                TotalPages = paged.TotalPages,
                HasPreviousPage = paged.HasPreviousPage,
                HasNextPage = paged.HasNextPage
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
