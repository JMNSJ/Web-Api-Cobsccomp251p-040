namespace SlseaSolarApi.Api.Application.Common;

/// <summary>
/// The pagination envelope returned by every list endpoint (design decision D11). It carries the
/// page of data plus <c>totalCount</c> and navigational <c>next</c>/<c>prev</c> links, so consumers
/// never have to construct URLs themselves.
/// </summary>
/// <typeparam name="T">The DTO element type.</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Data { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public PagedLinks Links { get; init; } = new();
}

/// <summary>Navigation links for a <see cref="PagedResult{T}"/>.</summary>
public class PagedLinks
{
    /// <summary>The URL of the page just returned.</summary>
    public string Self { get; init; } = string.Empty;

    /// <summary>The next page, or <c>null</c> when this is the last page.</summary>
    public string? Next { get; init; }

    /// <summary>The previous page, or <c>null</c> when this is the first page.</summary>
    public string? Prev { get; init; }
}