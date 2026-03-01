using System.Text;
using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>
/// 【发送策略实现】HTTP REST 发送策略
/// 将采集数据以POST请求发送到指定的HTTP接口
/// 
/// 通道配置说明：
///   Endpoint: 目标URL，如 "https://api.example.com/data/upload"
///   ConfigJson: { "method": "POST", "token": "Bearer xxx", "timeout": 5000 }
/// </summary>
public class HttpSendStrategy : ISendStrategy
{
    private readonly ILogger<HttpSendStrategy> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private string _endpoint = string.Empty;
    private string? _authToken;
    private int _timeoutMs = 5000;

    public HttpSendStrategy(ILogger<HttpSendStrategy> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public string ProtocolName => "Http";

    /// <inheritdoc/>
    public Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        _endpoint = channel.Endpoint;

        // 解析额外HTTP配置（认证token、超时等）
        if (!string.IsNullOrEmpty(channel.ConfigJson))
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(channel.ConfigJson);
            if (config != null)
            {
                if (config.TryGetValue("token", out var tokenEl))
                    _authToken = tokenEl.GetString();
                if (config.TryGetValue("timeout", out var timeoutEl))
                    _timeoutMs = timeoutEl.GetInt32();
            }
        }

        _logger.LogInformation("HTTP通道 [{ChannelName}] 初始化完成 -> {Endpoint}", channel.Name, _endpoint);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
    {
        try
        {
            // 构建发送Payload（与MQTT类似，支持别名映射）
            var aliasMap = package.Mappings
                .Where(m => m.IsEnabled)
                .ToDictionary(m => m.DataPointId, m => m.AliasName);

            var payload = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                channelCode = package.Channel.Code,
                data = package.DataList
                    .Where(d => d.Quality == DataQuality.Good)
                    .Select(d => new
                    {
                        name = aliasMap.TryGetValue(d.DataPointId, out var alias) && !string.IsNullOrEmpty(alias)
                            ? alias : d.Tag,
                        value   = d.Value,
                        unit    = string.Empty, // 可从DataPoint获取
                        quality = d.Quality.ToString()
                    })
            };

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 使用HttpClientFactory获取客户端（支持连接池复用）
            var httpClient = _httpClientFactory.CreateClient("GatewayHttpClient");
            httpClient.Timeout = TimeSpan.FromMilliseconds(_timeoutMs);

            // 添加认证头
            if (!string.IsNullOrEmpty(_authToken))
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

            var response = await httpClient.PostAsync(_endpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("HTTP发送成功 -> {Endpoint}, 状态码: {Code}", _endpoint, (int)response.StatusCode);
                return SendResult.Success(package.DataList.Count());
            }
            else
            {
                var error = $"HTTP响应失败，状态码: {(int)response.StatusCode}";
                _logger.LogWarning("{Error}", error);
                return SendResult.Failure(error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP发送异常，Endpoint: {Endpoint}", _endpoint);
            return SendResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public Task DisposeAsync()
    {
        // HttpClient 由 IHttpClientFactory 管理生命周期，无需手动释放
        return Task.CompletedTask;
    }
}
