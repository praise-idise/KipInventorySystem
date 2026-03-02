namespace KipInventorySystem.Shared.Models;

public class ApiResponse<T>(bool Success, int StatusCode, string? Message, T? Data)
{
    public bool Success { get; set; } = Success;
    public int StatusCode { get; set; } = StatusCode;
    public string? Message { get; set; } = Message;
    public T? Data { get; set; } = Data;
}