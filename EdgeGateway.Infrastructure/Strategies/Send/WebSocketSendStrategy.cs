using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.WebSocket;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>
/// 【发送策略实现】WebSocket 服务端推送策略
/// 
/// 作为 WebSocket 服务器，等待客户端连接，并将采集数据推送给订阅的客户端
/// 
/// 通道配置说明：
///   Endpoint: WebSocket 路径，如 "/ws"
///   ConfigJson: {
///     "port": 5000,                    // WebSocket 服务端口（可选，默认使用 ASP.NET Core 监听端口）
///     "subscribeTopic": "device/data", // 默认订阅主题
///     "heartbeatInterval": 30000       // 心跳间隔（毫秒）
///   }
/// </summary>
public class WebSocketSendStrategy : ISendStrategy
{
    private readonly ILogger<WebSocketSendStrategy> _logger;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly ISendStrategyRegistry _sendStrategyRegistry;

    private string _subscribeTopic = "device/data";
    private int _heartbeatIntervalMs = 30000;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    public WebSocketSendStrategy(
        ILogger<WebSocketSendStrategy> logger,
        WebSocketConnectionManager connectionManager,
        ISendStrategyRegistry sendStrategyRegistry)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _sendStrategyRegistry = sendStrategyRegistry;
    }

    /// <inheritdoc/>
    public string ProtocolName => "WebSocket";

    /// <inheritdoc/>
    public Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        // 解析配置
        if (!string.IsNullOrEmpty(channel.ConfigJson))
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(channel.ConfigJson);
            if (config != null)
            {
                if (config.TryGetValue("subscribeTopic", out var topicEl))
                {
                    var topic = topicEl.GetString();
                    if (!string.IsNullOrEmpty(topic))
                    {
                        _subscribeTopic = topic;
                        _logger.LogInformation("从 ConfigJson 读取订阅主题：{Topic}", topic);
                    }
                }

                if (config.TryGetValue("heartbeatInterval", out var heartbeatEl))
                    _heartbeatIntervalMs = heartbeatEl.GetInt32();
            }
        }
        else
        {
            _logger.LogWarning("通道 {ChannelName} 的 ConfigJson 为空，使用默认订阅主题：{DefaultTopic}", 
                channel.Name, _subscribeTopic);
        }

        _logger.LogInformation(
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            "WebSocket 服务端策略已启动\n" +
            "  通道：{ChannelName}\n" +
            "  订阅地址：ws://<host>:5000/ws\n" +
            "  订阅主题：{Topic}\n" +
            "  心跳间隔：{Interval}ms\n" +
            "  \n" +
            "  客户端连接示例:\n" +
            "    Apifox: ws://localhost:5000/ws?topic={Topic}\n" +
            "    Header: X-Subscribe-Topic: {Topic}\n" +
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━",
            channel.Name, _subscribeTopic, _heartbeatIntervalMs, _subscribeTopic, _subscribeTopic);

        // 启动心跳任务
        StartHeartbeat();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 启动心跳任务，定期向所有客户端发送 Ping
    /// </summary>
    private void StartHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts = new CancellationTokenSource();

        _heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(_heartbeatIntervalMs, _heartbeatCts.Token);
                    
                    if (_connectionManager.Count > 0)
                    {
                        _logger.LogDebug("向 {Count} 个客户端发送心跳", _connectionManager.Count);
                        await _connectionManager.BroadcastAsync(
                            JsonSerializer.Serialize(new 
                            { 
                                type = "ping", 
                                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() 
                            }));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("心跳任务已停止");
            }
        }, _heartbeatCts.Token);

        _logger.LogDebug("WebSocket 心跳任务已启动，间隔：{Interval}ms", _heartbeatIntervalMs);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 将采集数据推送给所有订阅了该主题的 WebSocket 客户端
    /// </remarks>
    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
    {
        try
        {
            // 构建映射字典：DataPointId → AliasName
            var aliasMap = package.Mappings
                .Where(m => m.IsEnabled)
                .ToDictionary(m => m.DataPointId, m => m.AliasName);

            // 构建推送数据
            var payload = new
            {
                type = "data",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                channelCode = package.Channel.Code,
                channelName = package.Channel.Name,
                data = package.DataList
                    .Where(d => d.Quality == DataQuality.Good)
                    .Select(d => new
                    {
                        name = aliasMap.TryGetValue(d.DataPointId, out var alias) && !string.IsNullOrEmpty(alias)
                            ? alias : d.Tag,
                        value = d.Value,
                        quality = d.Quality.ToString()
                    })
            };

            var json = JsonSerializer.Serialize(payload);

            // 推送给订阅该主题的客户端
            await _connectionManager.BroadcastToTopicAsync(_subscribeTopic, json);

            // 打印推送日志
            var wsAddress = $"ws://<host>:5000/ws";
            _logger.LogDebug(
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                "WebSocket 数据推送完成\n" +
                "  通道：{ChannelName}\n" +
                "  订阅地址：ws://<host>:5000/ws?topic={Topic}\n" +
                "  推送数据条数：{Count}\n" +
                "  当前在线客户端：{Clients}\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━",
                package.Channel.Name, _subscribeTopic, package.DataList.Count(), _connectionManager.Count);

            return SendResult.Success(package.DataList.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket 推送失败，通道：{Channel}", package.Channel.Name);
            return SendResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        // 停止心跳
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();

        // 关闭所有客户端连接
        await _connectionManager.CloseAllAsync();

        _logger.LogInformation("WebSocket 服务端策略已停止");
    }
}
