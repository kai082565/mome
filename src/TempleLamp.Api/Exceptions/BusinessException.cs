namespace TempleLamp.Api.Exceptions;

/// <summary>
/// 業務邏輯例外
/// </summary>
public class BusinessException : Exception
{
    public string ErrorCode { get; }
    public int HttpStatusCode { get; }

    public BusinessException(string errorCode, string message, int httpStatusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }
}

/// <summary>
/// 資源未找到例外
/// </summary>
public class NotFoundException : BusinessException
{
    public NotFoundException(string errorCode, string message)
        : base(errorCode, message, 404)
    {
    }
}

/// <summary>
/// 資源衝突例外（如燈位已鎖定）
/// </summary>
public class ConflictException : BusinessException
{
    public ConflictException(string errorCode, string message)
        : base(errorCode, message, 409)
    {
    }
}
