namespace TempleLamp.Desktop.Models;

/// <summary>
/// API 統一回應格式
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// API 統一回應格式（含資料）
/// </summary>
public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }
}
