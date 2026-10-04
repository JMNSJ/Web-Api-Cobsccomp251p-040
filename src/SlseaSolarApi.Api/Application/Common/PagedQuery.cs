namespace SlseaSolarApi.Api.Application.Common;

/// <summary>
/// Query-string paging/sorting/filtering options shared by every list endpoint.
/// Defaults follow the project conventions: <c>page=1</c>, <c>pageSize=50</c>, max <c>200</c>,
/// <c>sort=timestamp</c>, <c>order=desc</c>.
/// </summary>
public class PagedQuery
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values below 1 are clamped to 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Items per page. Clamped to the range 1..<see cref="MaxPageSize"/>.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Number of rows to skip, derived from <see cref="Page"/> and <see cref="PageSize"/>.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>Field to sort by. Interpretation is per-endpoint.</summary>
    public string? Sort { get; set; }

    /// <summary>Sort direction: <c>asc</c> or <c>desc</c>. Anything else is treated as <c>desc</c>.</summary>
    public string? Order { get; set; }

    /// <summary>True when the caller explicitly asked for ascending order.</summary>
    public bool IsAscending =>
        string.Equals(Order, "asc", StringComparison.OrdinalIgnoreCase);
}