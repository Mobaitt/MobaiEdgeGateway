using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Http;

/// <summary>
/// HTTP 监听服务 - 用于服务端模式，等待客户端来采集数据
/// 使用 ASP.NET Core 中间件方式运行，复用 Web 服务器端口
/// </summary>
public interface IHttpListenerService
{
    void RegisterEndpoint(string path);
    void UpdateData(string path, string jsonData);
    Task StopAsync(string path);
}

/// <summary>
/// HTTP 监听服务实现（ASP.NET Core 中间件方式）
/// </summary>
public class HttpListenerService : IHttpListenerService, IDisposable
{
    private readonly ILogger<HttpListenerService> _logger;
    private readonly Dictionary<string, string> _dataCache = new();
    private readonly Dictionary<string, bool> _registeredEndpoints = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private bool _disposed;

    public HttpListenerService(ILogger<HttpListenerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void RegisterEndpoint(string path)
    {
        _registeredEndpoints[path] = true;
        _logger.LogInformation("HTTP 服务端点已注册：{Path}，当前已注册端点：{Count}", path, _registeredEndpoints.Count);
    }

    /// <inheritdoc/>
    public async void UpdateData(string path, string jsonData)
    {
        await _cacheLock.WaitAsync();
        try
        {
            _dataCache[path] = jsonData;
            _logger.LogDebug("HTTP 数据已更新：{Path}, 数据长度：{Length}", path, jsonData.Length);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(string path)
    {
        // 注销端点
        _registeredEndpoints.Remove(path);
        
        // 同时清理缓存数据
        if (_dataCache.ContainsKey(path))
        {
            _dataCache.Remove(path);
        }
        
        _logger.LogInformation("HTTP 服务端点已注销：{Path}", path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理 HTTP 请求
    /// </summary>
    public async Task HandleRequestAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        // 只处理 GET 请求
        if (method != "GET")
        {
            context.Response.StatusCode = 405; // Method Not Allowed
            await context.Response.WriteAsync($"Method Not Allowed: {method}");
            return;
        }

        await _cacheLock.WaitAsync();
        try
        {
            if (_dataCache.TryGetValue(path, out var jsonData))
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(jsonData);
            }
            else if (_registeredEndpoints.ContainsKey(path))
            {
                // 端点已注册但暂无数据
                var noData = JsonSerializer.Serialize(new
                {
                    error = "No data available yet",
                    path = path,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync(noData);
            }
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Not Found");
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cacheLock.Dispose();
        _disposed = true;
    }
}
