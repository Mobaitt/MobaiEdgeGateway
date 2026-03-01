using System.Net.WebSockets;
using System.Text.Json;
using EdgeGateway.Infrastructure.WebSocket;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.WebSocket;

/// <summary>
/// WebSocket 服务端中间件 - 处理 WebSocket 连接请求
/// </summary>
public class WebSocketServerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketServerMiddleware> _logger;
    private readonly string _path;

    public WebSocketServerMiddleware(RequestDelegate next, ILogger<WebSocketServerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _path = "/ws";
    }

    public async Task InvokeAsync(HttpContext context, WebSocketConnectionManager connectionManager)
    {
        // 记录所有请求，方便调试
        _logger.LogDebug("收到请求：{Method} {Path}", context.Request.Method, context.Request.Path);

        // 只处理 WebSocket 路径
        if (context.Request.Path == _path)
        {
            // 记录请求头信息，帮助调试
            _logger.LogInformation(
                "收到 /ws 请求\n" +
                "  Method: {Method}\n" +
                "  IsWebSocketRequest: {IsWebSocket}\n" +
                "  Headers: {Headers}",
                context.Request.Method,
                context.WebSockets.IsWebSocketRequest,
                string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}: {h.Value}")));

            if (context.WebSockets.IsWebSocketRequest)
            {
                await HandleWebSocketRequestAsync(context, connectionManager);
                return;
            }
            else
            {
                _logger.LogWarning(
                    "请求路径匹配但不是 WebSocket 请求\n" +
                    "  可能原因：\n" +
                    "  1. 缺少 Connection: Upgrade 头\n" +
                    "  2. 缺少 Upgrade: websocket 头\n" +
                    "  3. 使用了错误的协议（HTTP 而不是 WebSocket）");
                
                // 返回错误提示
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                var error = System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "Bad Request",
                    message = "This endpoint requires a WebSocket connection",
                    hint = "Please use ws:// or wss:// protocol instead of http://"
                });
                await context.Response.WriteAsync(error);
                return;
            }
        }

        await _next(context);
    }

    private async Task HandleWebSocketRequestAsync(HttpContext context, WebSocketConnectionManager connectionManager)
    {
        var clientId = Guid.NewGuid().ToString("N")[..8];
        System.Net.WebSockets.WebSocket? webSocket = null;
        WebSocketClient? client = null;

        try
        {
            // 接受 WebSocket 连接
            webSocket = await context.WebSockets.AcceptWebSocketAsync();

            // 获取订阅主题（从 Query 或 Header）
            string? subscribeTopic = null;
            if (context.Request.Headers.TryGetValue("X-Subscribe-Topic", out var topicHeader))
            {
                subscribeTopic = topicHeader.ToString();
            }
            else if (context.Request.Query.TryGetValue("topic", out var topicQuery))
            {
                subscribeTopic = topicQuery.ToString();
            }

            // 创建客户端包装对象
            client = new WebSocketClient(webSocket, clientId, _logger);
            if (!string.IsNullOrEmpty(subscribeTopic))
            {
                client.SetSubscribeTopic(subscribeTopic);
            }

            // 添加到连接管理器
            connectionManager.AddClient(clientId, client);

            // 打印客户端连接信息
            var host = context.Request.Host.ToString();
            var path = context.Request.Path;
            var wsUrl = $"{(context.Request.IsHttps ? "wss" : "ws")}://{host}{path}";
            // 发送欢迎消息
            var welcomeMsg = new
            {
                type = "welcome",
                clientId = clientId,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                message = "欢迎连接到 EdgeGateway WebSocket 服务器",
                serverInfo = new
                {
                    wsUrl = wsUrl,
                    subscribeTopic = subscribeTopic ?? "device/data",
                    availableTopics = new[] { "device/data", "device/command", "device/config" }
                }
            };
            await client.SendAsync(JsonSerializer.Serialize(welcomeMsg));

            // 启动接收循环，处理客户端消息
            await ReceiveLoopAsync(client, connectionManager, _logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "WebSocket 连接异常：{ClientId}", clientId);
        }
        finally
        {
            // 清理连接
            if (client != null)
            {
                await connectionManager.RemoveClientAsync(clientId);
            }
            else if (webSocket != null)
            {
                webSocket.Dispose();
            }
        }
    }

    /// <summary>
    /// 接收循环 - 处理客户端发送的消息
    /// </summary>
    private async Task ReceiveLoopAsync(WebSocketClient client, WebSocketConnectionManager connectionManager, ILogger logger)
    {
        var buffer = new byte[4096];

        try
        {
            while (client.WebSocket.State == WebSocketState.Open)
            {
                var result = await client.WebSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogInformation("客户端 {ClientId} 发送关闭请求", client.ClientId);
                    break;
                }

                if (result.Count > 0)
                {
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleClientMessageAsync(client, message, connectionManager, logger);
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            logger.LogInformation("客户端 {ClientId} 连接意外关闭", client.ClientId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "客户端 {ClientId} 接收消息异常", client.ClientId);
        }
    }

    /// <summary>
    /// 处理客户端消息
    /// </summary>
    private async Task HandleClientMessageAsync(
        WebSocketClient client, 
        string message, 
        WebSocketConnectionManager connectionManager,
        ILogger logger)
    {
        try
        {
            logger.LogDebug("客户端 {ClientId} 消息：{Message}", client.ClientId, message);

            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeProp))
            {
                var messageType = typeProp.GetString()?.ToLower();

                switch (messageType)
                {
                    case "ping":
                        // 响应心跳
                        var pong = new { type = "pong", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await client.SendAsync(JsonSerializer.Serialize(pong));
                        break;

                    case "subscribe":
                        // 客户端订阅主题
                        if (root.TryGetProperty("topic", out var topicProp))
                        {
                            var topic = topicProp.GetString();
                            if (!string.IsNullOrEmpty(topic))
                            {
                                client.SetSubscribeTopic(topic);
                                var ack = new { type = "ack", action = "subscribe", topic };
                                await client.SendAsync(JsonSerializer.Serialize(ack));
                            }
                        }
                        break;

                    case "unsubscribe":
                        // 客户端取消订阅
                        client.SetSubscribeTopic(string.Empty);
                        var unsubAck = new { type = "ack", action = "unsubscribe" };
                        await client.SendAsync(JsonSerializer.Serialize(unsubAck));
                        break;

                    case "data":
                        // 客户端上报数据（如果需要处理）
                        logger.LogInformation("客户端 {ClientId} 上报数据", client.ClientId);
                        break;

                    default:
                        logger.LogWarning("未知消息类型：{Type}", messageType);
                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "解析客户端 {ClientId} 消息失败：{Message}", client.ClientId, message);
        }
    }
}

/// <summary>
/// WebSocket 中间件扩展方法
/// </summary>
public static class WebSocketServerMiddlewareExtensions
{
    public static IApplicationBuilder UseWebSocketServer(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<WebSocketServerMiddleware>();
    }
}
