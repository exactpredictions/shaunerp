namespace ShaunERP.Core.Platform;

/// <summary>
/// Server-side list query: page sizes are restricted to 50 / 100 / 200 for ERP-scale tables.
/// </summary>
public class ListQuery
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => NormalizePageSize(PageSize);
    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
    public int Take => NormalizedPageSize;

    public static int NormalizePageSize(int pageSize) =>
        pageSize switch
        {
            100 => 100,
            200 => 200,
            _ => 50
        };

    public ListQuery Clone() => new()
    {
        Search = Search,
        Page = NormalizedPage,
        PageSize = NormalizedPageSize
    };

    public ListQuery WithPage(int page) => new()
    {
        Search = Search,
        Page = page < 1 ? 1 : page,
        PageSize = NormalizedPageSize
    };

    public ListQuery WithPageSize(int pageSize) => new()
    {
        Search = Search,
        Page = 1,
        PageSize = NormalizePageSize(pageSize)
    };

    public ListQuery WithSearch(string? search) => new()
    {
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
        Page = 1,
        PageSize = NormalizedPageSize
    };
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page * PageSize < TotalCount;
    public int FromRow => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int ToRow => Math.Min(Page * PageSize, TotalCount);

    public static PagedResult<T> Empty(ListQuery query) => new()
    {
        Items = Array.Empty<T>(),
        TotalCount = 0,
        Page = query.NormalizedPage,
        PageSize = query.NormalizedPageSize
    };
}
