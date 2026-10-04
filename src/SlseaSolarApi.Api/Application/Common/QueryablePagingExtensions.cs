using Microsoft.EntityFrameworkCore;

namespace SlseaSolarApi.Api.Application.Common;

/// <summary>
/// Turns an <see cref="IQueryable{T}"/> into a <see cref="PagedResult{T}"/> with correct links.
/// </summary>
public static class QueryablePagingExtensions
{
    /// <summary>
    /// Materialises one page and builds the pagination envelope. <paramref name="linkBuilder"/> is
    /// given a page number and returns the absolute-or-relative URL for it, keeping this helper
    /// independent of how a given endpoint shapes its query string.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        PagedQuery query,
        Func<int, string> linkBuilder,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await source.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var data = await source
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Data = data,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Links = new PagedLinks
            {
                Self = linkBuilder(query.Page),
                Next = query.Page < totalPages ? linkBuilder(query.Page + 1) : null,
                Prev = query.Page > 1 ? linkBuilder(query.Page - 1) : null
            }
        };
    }
}