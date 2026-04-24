using System.Text.Json.Serialization;

namespace KipInventorySystem.Shared.Models;

public class ApiResponse<T>(bool Success, int StatusCode, string? Message, T? Data, PaginationMeta? Pagination = null)
{
    public bool Success { get; set; } = Success;
    public int StatusCode { get; set; } = StatusCode;
    public string? Message { get; set; } = Message;
    public T? Data { get; set; } = Data;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationMeta? Pagination { get; set; } = Pagination;
}

public record PaginationMeta(int CurrentPage, int PageSize, int TotalRecords, int TotalPages);