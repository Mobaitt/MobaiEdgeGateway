using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.WebSocket;

/// <summary>
/// WebSocket 连接管理器 - 管理所有连接到服务器的客户端
/// </summary>
public class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocketClient> _clients = new();
    private readonly ILogger<WebSocketConnectionManager> _logger;

    public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 添加新的客户端连接
    /// </summary>
    public void AddClient(string clientId, WebSocketClient client)
    {
        _clients.TryAdd(clientId, client);
        _logger.LogInformation("客户端已连接：{ClientId}，当前在线：{Count}", clientId, _clients.Count);
    }

    /// <summary>
    /// 移除客户端连接
    /// </summary>
    public async Task RemoveClientAsync(string clientId)
    {
        if (_clients.TryRemove(clientId, out var client))
        {
            await client.CloseAsync();
            _logger.LogInformation("客户端已断开：{ClientId}，当前在线：{Count}", clientId, _clients.Count);
        }
    }

    /// <summary>
    /// 获取所有客户端 ID
    /// </summary>
    public IEnumerable<string> GetAllClientIds() => _clients.Keys;

    /// <summary>
    /// 获取指定客户端
    /// </summary>
    public WebSocketClient? GetClient(string clientId) => 
        _clients.TryGetValue(clientId, out var client) ? client : null;

    /// <summary>
    /// 获取订阅了指定主题的客户端
    /// </summary>
    public IEnumerable<WebSocketClient> GetClientsByTopic(string topic) =>
        _clients.Values.Where(c => string.Equals(c.SubscribeTopic, topic, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 广播消息给所有客户端
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        var tasks = _clients.Values.Select(c => c.SendAsync(message));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 广播消息给订阅指定主题的所有客户端
    /// </summary>
    public async Task BroadcastToTopicAsync(string topic, string message)
    {
        var clients = GetClientsByTopic(topic);
        var clientList = clients.ToList();
        var tasks = clientList.Select(c => c.SendAsync(message));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 发送消息给指定客户端
    /// </summary>
    public async Task SendToClientAsync(string clientId, string message)
    {
        if (GetClient(clientId) is var client && client != null)
        {
            await client.SendAsync(message);
        }
    }

    /// <summary>
    /// 获取在线客户端数量
    /// </summary>
    public int Count => _clients.Count;

    /// <summary>
    /// 关闭所有连接
    /// </summary>
    public async Task CloseAllAsync()
    {
        var tasks = _clients.Values.Select(c => c.CloseAsync());
        await Task.WhenAll(tasks);
        _clients.Clear();
        _logger.LogInformation("所有 WebSocket 连接已关闭");
    }
}

/// <summary>
/// WebSocket 客户端连接信息
/// </summary>
public class WebSocketClient
{
    private readonly System.Net.WebSockets.WebSocket _webSocket;
    private readonly string _clientId;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private bool _isClosed;

    public System.Net.WebSockets.WebSocket WebSocket => _webSocket;
    public string ClientId => _clientId;
    public string? SubscribeTopic { get; private set; }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;

    public WebSocketClient(System.Net.WebSockets.WebSocket webSocket, string clientId, ILogger logger)
    {
        _webSocket = webSocket;
        _clientId = clientId;
        _logger = logger;
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// 设置订阅主题
    /// </summary>
    public void SetSubscribeTopic(string topic)
    {
        SubscribeTopic = topic;
        _logger.LogDebug("客户端 {ClientId} 订阅主题：{Topic}", _clientId, topic);
    }

    /// <summary>
    /// 发送消息给客户端
    /// </summary>
    public async Task SendAsync(string message)
    {
        if (_isClosed || _webSocket.State != WebSocketState.Open)
            return;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向客户端 {ClientId} 发送消息失败", _clientId);
        }
    }

    /// <summary>
    /// 发送 Ping 心跳
    /// </summary>
    public async Task SendPingAsync()
    {
        var ping = new { type = "ping", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        var json = JsonSerializer.Serialize(ping);
        await SendAsync(json);
    }

    /// <summary>
    /// 发送命令给客户端
    /// </summary>
    public async Task SendCommandAsync(string command, object? data = null)
    {
        object msg;
        if (data == null)
        {
            msg = new { type = "command", command };
        }
        else
        {
            msg = new { type = "command", command, data };
        }
        var json = JsonSerializer.Serialize(msg);
        await SendAsync(json);
    }

    /// <summary>
    /// 发送配置更新给客户端
    /// </summary>
    public async Task SendConfigAsync(object config)
    {
        var msg = new { type = "config", data = config };
        var json = JsonSerializer.Serialize(msg);
        await SendAsync(json);
    }

    /// <summary>
    /// 关闭连接
    /// </summary>
    public async Task CloseAsync()
    {
        if (_isClosed) return;

        _isClosed = true;
        _cts.Cancel();

        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭客户端 {ClientId} 连接时出错", _clientId);
        }
        finally
        {
            _webSocket.Dispose();
            _cts.Dispose();
        }
    }
}
