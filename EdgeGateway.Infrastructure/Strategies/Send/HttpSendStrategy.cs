using System.Text;
using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>
/// 【发送策略实现】HTTP REST 发送策略
///
/// 支持两种模式：
/// 1. 客户端模式：主动将数据 POST 到目标 HTTP 接口
/// 2. 服务端模式：作为 HTTP 服务器，等待客户端来采集数据（复用 Web 服务器端口）
///
/// 通道配置说明：
///   Endpoint: 
///     - 客户端模式：目标 URL，如 "https://api.example.com/data/upload"
///     - 服务端模式：监听路径，如 "/api/data"
///   ConfigJson: {
///     "mode": "client",                    // "client" 或 "server"
///     "method": "POST",                    // 客户端模式：HTTP 方法
///     "token": "Bearer xxx",               // 客户端模式：认证令牌
///     "timeout": 5000                      // 客户端模式：超时毫秒
///   }
/// </summary>
public class HttpSendStrategy : ISendStrategy
{
    private readonly ILogger<HttpSendStrategy> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpListenerService _httpListenerService;

    private string _endpoint = string.Empty;
    private string _mode = "client"; // "client" 或 "server"
    private string? _authToken;
    private int _timeoutMs = 5000;

    public HttpSendStrategy(
        ILogger<HttpSendStrategy> logger,
        IHttpClientFactory httpClientFactory,
        IHttpListenerService httpListenerService)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _httpListenerService = httpListenerService;
    }

    /// <inheritdoc/>
    public string ProtocolName => "Http";

    /// <inheritdoc/>
    public async Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        _endpoint = channel.Endpoint;
        _mode = channel.HttpMode ?? "client";
        _authToken = channel.HttpToken;
        _timeoutMs = channel.HttpTimeout ?? 5000;

        if (_mode == "server")
        {
            // 服务端模式：注册端点（复用 Web 服务器端口）
            // 自动添加 /api/http-data 前缀
            var endpointPath = _endpoint.StartsWith("/api/http-data/", StringComparison.OrdinalIgnoreCase) 
                ? _endpoint 
                : $"/api/http-data/{_endpoint.TrimStart('/')}";
            
            _httpListenerService.RegisterEndpoint(endpointPath);
            _logger.LogInformation(
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                "HTTP 服务端模式已启动（复用 Web 端口）\n" +
                "  通道：{ChannelName}\n" +
                "  数据访问：http://localhost:5000{Endpoint}\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━",
                channel.Name, endpointPath);
        }
        else
        {
            // 客户端模式
            _logger.LogInformation("HTTP 客户端模式已初始化 -> {Endpoint}", _endpoint);
        }
    }

    /// <summary>
    /// 重新配置端点路径（用于通道更新时）
    /// </summary>
    public async Task ReconfigureEndpointAsync(Channel channel, string oldEndpoint, CancellationToken cancellationToken = default)
    {
        // 如果是服务端模式且路径发生变化，先注销旧路径
        if (_mode == "server" && !string.IsNullOrEmpty(oldEndpoint) && oldEndpoint != channel.Endpoint)
        {
            var oldPath = oldEndpoint.StartsWith("/api/http-data/", StringComparison.OrdinalIgnoreCase)
                ? oldEndpoint
                : $"/api/http-data/{oldEndpoint.TrimStart('/')}";

            await _httpListenerService.StopAsync(oldPath);
        }

        // 重新初始化
        await InitializeAsync(channel, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 构建发送 Payload（包含所有数据点，不过滤质量）
    /// 服务端模式：更新缓存数据，等待客户端来取
    /// 客户端模式：主动 POST 到目标地址
    /// </remarks>
    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
    {
        try
        {
            // 构建统一格式的数据
            // 格式：{ "name": "DEV_SIMULATOR_001.DEV_SIMULATOR_001.Temperature", "value": 61.42, "unit": "℃", "quality": "Good" }
            var payload = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                channelCode = package.Channel.Code,
                channelName = package.Channel.Name,
                data = package.DataList
                    .Select(d => new
                    {
                        name = d.Tag,  // 使用完整 Tag（设备编码。数据点 Tag）
                        value = d.Value,
                        unit = d.Unit ?? string.Empty,
                        quality = d.Quality.ToString()
                    })
            };

            var json = JsonSerializer.Serialize(payload);

            if (_mode == "server")
            {
                // 服务端模式：更新缓存数据
                // 使用与注册时相同的路径
                var endpointPath = _endpoint.StartsWith("/api/http-data/", StringComparison.OrdinalIgnoreCase)
                    ? _endpoint
                    : $"/api/http-data/{_endpoint.TrimStart('/')}";

                _httpListenerService.UpdateData(endpointPath, json);
                return SendResult.Success(package.DataList.Count());
            }
            else
            {
                // 客户端模式：主动 POST 到目标地址
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 使用 HttpClientFactory 获取客户端（支持连接池复用）
                var httpClient = _httpClientFactory.CreateClient("GatewayHttpClient");
                httpClient.Timeout = TimeSpan.FromMilliseconds(_timeoutMs);

                // 添加认证头
                if (!string.IsNullOrEmpty(_authToken))
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

                var response = await httpClient.PostAsync(_endpoint, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return SendResult.Success(package.DataList.Count());
                }
                else
                {
                    var error = $"HTTP 响应失败，状态码：{(int)response.StatusCode}";
                    _logger.LogWarning("{Error}", error);
                    return SendResult.Failure(error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP 发送异常，Endpoint: {Endpoint}", _endpoint);
            return SendResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_mode == "server")
        {
            await _httpListenerService.StopAsync(_endpoint);
        }
    }
}
