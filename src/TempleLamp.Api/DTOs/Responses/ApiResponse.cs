namespace TempleLamp.Api.DTOs.Responses;

/// <summary>
/// 統一 API 回應格式
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data
        };
    }

    public static ApiResponse<T> Fail(string errorCode, string errorMessage)
    {
        return new ApiResponse<T>
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// 無資料回應
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok()
    {
        return new ApiResponse { Success = true };
    }

    public new static ApiResponse Fail(string errorCode, string errorMessage)
    {
        return new ApiResponse
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
