using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Platform;

namespace ShaunERP.Infrastructure.Persistence;

public static class PagingQueryableExtensions
{
    /// <summary>
    /// Escape user input for PostgreSQL ILIKE patterns.
    /// </summary>
    public static string? ToILikePattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var s = search.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{s}%";
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        ListQuery listQuery,
        CancellationToken ct = default)
    {
        listQuery ??= new ListQuery();
        var page = listQuery.NormalizedPage;
        var pageSize = listQuery.NormalizedPageSize;

        var total = await query.CountAsync(ct);
        if (total == 0)
        {
            return PagedResult<T>.Empty(listQuery);
        }

        var maxPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > maxPage)
        {
            page = maxPage;
        }

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
