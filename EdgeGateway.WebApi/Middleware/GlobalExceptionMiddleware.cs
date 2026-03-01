using System.Net;
using System.Text.Json;
using EdgeGateway.WebApi.DTOs.Response;

namespace EdgeGateway.WebApi.Middleware;

/// <summary>
/// 全局异常处理中间件
/// 捕获所有未处理的异常，统一返回 ApiResponse 格式的错误响应
/// 避免将内部异常细节直接暴露给客户端
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
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

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // 根据异常类型决定HTTP状态码
        var (statusCode, message) = ex switch
        {
            ArgumentException or InvalidOperationException =>
                (HttpStatusCode.BadRequest, ex.Message),

            KeyNotFoundException =>
                (HttpStatusCode.NotFound, ex.Message),

            NotSupportedException =>
                (HttpStatusCode.NotImplemented, ex.Message),

            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized, "无访问权限"),

            _ => (HttpStatusCode.InternalServerError, "服务器内部错误，请联系管理员")
        };

        // 服务器错误需要记录完整堆栈（业务错误只记录警告）
        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "未处理的服务器异常: {Path}", context.Request.Path);
        else
            _logger.LogWarning("业务异常 [{Code}] {Path}: {Message}",
                (int)statusCode, context.Request.Path, ex.Message);

        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = ApiResponse.Fail(message);
        var json     = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
