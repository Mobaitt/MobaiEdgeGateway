using EdgeGateway.Domain.Options;
using Microsoft.Extensions.Options;

namespace EdgeGateway.WebApi.Middleware;

/// <summary>
/// 演示模式中间件 - 在演示模式下禁止所有修改操作（POST/PUT/DELETE/PATCH）
/// </summary>
public class DemoModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DemoModeOptions _options;
    private readonly ILogger<DemoModeMiddleware> _logger;

    // 允许修改的只读路径（白名单）
    private static readonly string[] AllowedWritePaths = new[]
    {
        "/api/http-data"  // HTTP 服务端模式数据接收接口
    };

    public DemoModeMiddleware(
        RequestDelegate next,
        IOptions<DemoModeOptions> options,
        ILogger<DemoModeMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 记录当前演示模式状态
        // _logger.LogInformation("演示模式中间件：Enabled={Enabled}, Message={Message}", _options.Enabled, _options.Message);

        // 如果未启用演示模式，直接跳过
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method.ToUpperInvariant();
        var path = context.Request.Path.Value;

        // 检查是否是修改操作（POST/PUT/DELETE/PATCH）
        if (IsWriteMethod(method) && !IsAllowedPath(path))
        {
            _logger.LogWarning("演示模式下拦截修改请求：{Method} {Path}", method, path);

            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            var response = new
            {
                success = false,
                message = _options.Message,
                code = "DEMO_MODE_READONLY"
            };
            await context.Response.WriteAsJsonAsync(response);
            return;
        }

        await _next(context);
    }

    private static bool IsWriteMethod(string method)
    {
        return method is "POST" or "PUT" or "DELETE" or "PATCH";
    }

    private static bool IsAllowedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return AllowedWritePaths.Any(allowed =>
            path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
    }
}
