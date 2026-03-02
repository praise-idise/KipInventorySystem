namespace KipInventorySystem.Shared.Models;

public class PaginationResult<T>
{
    public IReadOnlyList<T> Records { get; set; } = [];
    public int TotalRecords { get; set; }
    public int PageSize { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);
}
