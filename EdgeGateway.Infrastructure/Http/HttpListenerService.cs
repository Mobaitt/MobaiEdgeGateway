using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Http;

public interface IHttpListenerService
{
    void RegisterEndpoint(string path);
    void UpdateData(string path, string jsonData);
    Task StopAsync(string path);
}

/// <summary>HTTP 服务端数据缓存。注册、更新、注销互斥，响应写入在锁外进行。</summary>
public class HttpListenerService : IHttpListenerService, IDisposable
{
    private readonly ILogger<HttpListenerService> _logger;
    private readonly Dictionary<string, string?> _endpoints = new();
    private readonly object _syncRoot = new();
    private bool _disposed;

    public HttpListenerService(ILogger<HttpListenerService> logger) => _logger = logger;

    public void RegisterEndpoint(string path)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _endpoints.TryAdd(path, null);
        }
        _logger.LogInformation("HTTP 服务端点已注册：{Path}", path);
    }

    public void UpdateData(string path, string jsonData)
    {
        lock (_syncRoot)
        {
            // 注销后的迟到更新不能重新创建端点。
            if (!_disposed && _endpoints.ContainsKey(path))
                _endpoints[path] = jsonData;
        }
    }

    public Task StopAsync(string path)
    {
        lock (_syncRoot) { _endpoints.Remove(path); }
        _logger.LogInformation("HTTP 服务端点已注销：{Path}", path);
        return Task.CompletedTask;
    }

    public async Task HandleRequestAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (context.Request.Method != "GET")
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            await context.Response.WriteAsync($"Method Not Allowed: {context.Request.Method}", context.RequestAborted);
            return;
        }

        bool registered;
        string? jsonData;
        lock (_syncRoot) { registered = _endpoints.TryGetValue(path, out jsonData); }
        if (!registered)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Not Found", context.RequestAborted);
            return;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = jsonData == null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
        await context.Response.WriteAsync(jsonData ?? JsonSerializer.Serialize(new
        {
            error = "No data available yet", path, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }), context.RequestAborted);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
            _endpoints.Clear();
        }
    }
}
