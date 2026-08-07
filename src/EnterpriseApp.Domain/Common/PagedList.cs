namespace EnterpriseApp.Domain.Common;

/// <summary>
/// Generic pagination container.
/// The static factory <c>Create</c> accepts pre-resolved items and totalCount so that
/// the Infrastructure layer can call <c>CountAsync / ToListAsync</c> (EF async) and pass
/// the results here — keeping Domain free of any async or EF dependency.
/// </summary>
public sealed class PagedList<T>(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
{
    public IReadOnlyList<T> Items      { get; } = items;
    public int TotalCount              { get; } = totalCount;
    public int Page                    { get; } = page;
    public int PageSize                { get; } = pageSize;
    public int TotalPages              { get; } = (int)Math.Ceiling(totalCount / (double)pageSize);
    public bool HasPreviousPage        => Page > 1;
    public bool HasNextPage            => Page < TotalPages;

    /// <summary>
    /// Creates a PagedList from already-resolved data.
    /// The caller (Repository) is responsible for running CountAsync and ToListAsync.
    /// </summary>
    public static PagedList<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        => new(items, totalCount, page, pageSize);
}
