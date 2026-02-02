using System.Net;
using System.Text.Json;
using TempleLamp.Api.DTOs.Responses;
using TempleLamp.Api.Exceptions;

namespace TempleLamp.Api.Middleware;

/// <summary>
/// 全域例外處理 Middleware
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json; charset=utf-8";

        ApiResponse errorResponse;
        int statusCode;

        switch (exception)
        {
            case NotFoundException notFoundEx:
                statusCode = (int)HttpStatusCode.NotFound;
                errorResponse = ApiResponse.Fail(notFoundEx.ErrorCode, notFoundEx.Message);
                _logger.LogWarning("資源未找到: {ErrorCode} - {Message}", notFoundEx.ErrorCode, notFoundEx.Message);
                break;

            case ConflictException conflictEx:
                statusCode = (int)HttpStatusCode.Conflict;
                errorResponse = ApiResponse.Fail(conflictEx.ErrorCode, conflictEx.Message);
                _logger.LogWarning("資源衝突: {ErrorCode} - {Message}", conflictEx.ErrorCode, conflictEx.Message);
                break;

            case BusinessException businessEx:
                statusCode = businessEx.HttpStatusCode;
                errorResponse = ApiResponse.Fail(businessEx.ErrorCode, businessEx.Message);
                _logger.LogWarning("業務錯誤: {ErrorCode} - {Message}", businessEx.ErrorCode, businessEx.Message);
                break;

            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse = ApiResponse.Fail(ErrorCodes.SYSTEM_ERROR, "系統發生錯誤，請稍後再試");
                _logger.LogError(exception, "未處理的系統錯誤");
                break;
        }

        response.StatusCode = statusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await response.WriteAsync(json);
    }
}
