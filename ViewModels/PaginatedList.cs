using Microsoft.EntityFrameworkCore;

namespace DentistDB.ViewModels;

public class PaginatedList<T> : List<T>
{
    public int PageIndex { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }
    public int PageSize { get; }

    public PaginatedList(IEnumerable<T> items, int totalCount, int pageIndex, int pageSize)
    {
        TotalCount = totalCount;
        PageSize = pageSize;
        PageIndex = pageIndex;
        TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        AddRange(items);
    }

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((PageIndex - 1) * PageSize) + 1;
    public int LastItemNumber => Math.Min(PageIndex * PageSize, TotalCount);

    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Max(1, pageSize);

        var totalCount = await source.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        if (totalPages > 0 && pageIndex > totalPages)
        {
            pageIndex = totalPages;
        }

        var items = await source
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<T>(items, totalCount, pageIndex, pageSize);
    }
}

public class PaginationViewModel
{
    public int PageIndex { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int FirstItemNumber { get; set; }
    public int LastItemNumber { get; set; }
}
