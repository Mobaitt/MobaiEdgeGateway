using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
                var error = JsonSerializer.Serialize(new
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

            // 验证订阅主题是否对应已启用的 WebSocket 通道
            if (!string.IsNullOrEmpty(subscribeTopic))
            {
                var isValidTopic = await ValidateSubscribeTopicAsync(context, subscribeTopic);
                if (!isValidTopic)
                {
                    _logger.LogWarning("客户端连接被拒绝：订阅主题 {Topic} 未配置或通道未启用", subscribeTopic);

                    // 返回错误提示
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    var error = JsonSerializer.Serialize(new
                    {
                        error = "Forbidden",
                        message = $"订阅主题 '{subscribeTopic}' 未配置或通道未启用",
                        hint = "请确保已创建 WebSocket 类型的发送通道并启用，且订阅主题匹配"
                    });
                    await context.Response.WriteAsync(error);
                    return;
                }
            }

            // 接受 WebSocket 连接
            webSocket = await context.WebSockets.AcceptWebSocketAsync();

            // 创建客户端包装对象
            client = new WebSocketClient(webSocket, clientId, _logger);
            if (!string.IsNullOrEmpty(subscribeTopic))
            {
                client.SetSubscribeTopic(subscribeTopic);
            }

            // 添加到连接管理器
            connectionManager.AddClient(clientId, client);

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
    /// 验证订阅主题是否对应已启用的 WebSocket 通道
    /// </summary>
    private async Task<bool> ValidateSubscribeTopicAsync(HttpContext context, string subscribeTopic)
    {
        try
        {
            // 从服务容器获取数据库上下文
            using var scope = context.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

            // 获取所有 WebSocket 通道到内存中进行比较（避免 EF Core 翻译问题）
            var channels = await dbContext.Channels
                .Where(c => c.IsEnabled && c.Protocol == SendProtocol.WebSocket)
                .ToListAsync();

            // 在内存中检查是否存在匹配的通道
            var isValid = channels.Any(c =>
                !string.IsNullOrEmpty(c.WsSubscribeTopic) &&
                string.Equals(c.WsSubscribeTopic, subscribeTopic, StringComparison.OrdinalIgnoreCase)
            );

            if (!isValid)
            {
                _logger.LogWarning(
                    "订阅主题验证失败：{Topic}\n可用通道：{Channels}",
                    subscribeTopic,
                    string.Join(", ", channels.Select(c => $"{c.Name}(主题：{c.WsSubscribeTopic ?? "未配置"})")));
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证订阅主题时出错：{Topic}", subscribeTopic);
            return false;
        }
    }

    /// <summary>
    /// 接收循环 - 处理客户端发送的消息
    /// </summary>
    private async Task ReceiveLoopAsync(WebSocketClient client, WebSocketConnectionManager connectionManager,
        ILogger logger)
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
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
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