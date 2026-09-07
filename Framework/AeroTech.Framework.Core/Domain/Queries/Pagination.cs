namespace AeroTech.Framework.Core.Domain.Queries;

public class PaginationQuery
{
    public string? SortBy { get; set; }
    public bool SortDirection { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PaginatedList<TItem>
{
    public static PaginatedList<TItem>
        Create(IEnumerable<TItem> items, int pageNumber, int pageSize, long totalCount) => new()
        {
            Results = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };

    public IEnumerable<TItem> Results { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
}

public class GridData<TData> : PaginatedList<TData>
{
    public static GridData<TData> Create(PaginatedList<TData> paginatedList) =>
        new()
        {
            Results = paginatedList.Results,
            PageNumber = paginatedList.PageNumber,
            PageSize = paginatedList.PageSize,
            TotalCount = paginatedList.TotalCount,
            Metadata = MetadataGenerator.Generate<TData>()
        };

    public GridMetadata Metadata { get; set; } = null!;
}

public class GridMetadata
{
    public ICollection<GridMetadataField> Fields { get; set; } = null!;
}

public class GridMetadataField
{
    public string Name { get; set; } = null!;
    public string Title { get; set; } = null!;
    public bool IsSortable { get; set; }
}
