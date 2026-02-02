using System.Text.Json;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Exceptions;

namespace TempleLamp.Api.Middleware;

/// <summary>
/// 工作站識別 Middleware
/// 從 Header 讀取 X-Workstation-Id 並存入 HttpContext.Items
/// </summary>
public class WorkstationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WorkstationMiddleware> _logger;

    public const string HeaderName = "X-Workstation-Id";
    public const string ContextKey = "WorkstationId";

    public WorkstationMiddleware(RequestDelegate next, ILogger<WorkstationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 排除 Swagger 相關路徑
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.StartsWith("/swagger") || path == "/" || path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        // 檢查 Header
        if (!context.Request.Headers.TryGetValue(HeaderName, out var workstationIdValue) ||
            string.IsNullOrWhiteSpace(workstationIdValue))
        {
            _logger.LogWarning("請求缺少 {HeaderName} Header", HeaderName);

            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json; charset=utf-8";

            var errorResponse = ApiResponse.Fail(
                ErrorCodes.WORKSTATION_REQUIRED,
                $"缺少必要的 Header: {HeaderName}"
            );

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
            return;
        }

        var workstationId = workstationIdValue.ToString().Trim();

        // 驗證格式（可選：限制長度或格式）
        if (workstationId.Length > 50)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json; charset=utf-8";

            var errorResponse = ApiResponse.Fail(
                ErrorCodes.INVALID_REQUEST,
                "WorkstationId 長度不可超過 50 字元"
            );

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
            return;
        }

        // 存入 HttpContext.Items
        context.Items[ContextKey] = workstationId;
        _logger.LogDebug("工作站識別: {WorkstationId}", workstationId);

        await _next(context);
    }
}

/// <summary>
/// HttpContext 擴充方法
/// </summary>
public static class HttpContextExtensions
{
    public static string GetWorkstationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(WorkstationMiddleware.ContextKey, out var value) &&
            value is string workstationId)
        {
            return workstationId;
        }

        throw new BusinessException(ErrorCodes.WORKSTATION_REQUIRED, "無法取得工作站識別碼");
    }
}
