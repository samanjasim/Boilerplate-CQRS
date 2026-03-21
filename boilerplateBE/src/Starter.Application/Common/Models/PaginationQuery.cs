namespace Starter.Application.Common.Models;

public abstract record PaginationQuery
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private int _pageNumber = DefaultPageNumber;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? DefaultPageNumber : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public string? SearchTerm { get; init; }
}
