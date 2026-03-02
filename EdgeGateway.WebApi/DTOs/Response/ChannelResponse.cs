namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>发送通道详情响应</summary>
public class ChannelResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>发送协议名称（字符串，如 "Mqtt"）</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>发送协议枚举值（数字）</summary>
    public int ProtocolValue { get; set; }

    /// <summary>连接地址（Endpoint）</summary>
    public string Endpoint { get; set; } = string.Empty;

    // MQTT 配置
    /// <summary>MQTT 主题</summary>
    public string? MqttTopic { get; set; }

    /// <summary>MQTT 客户端 ID</summary>
    public string? MqttClientId { get; set; }

    /// <summary>MQTT 用户名</summary>
    public string? MqttUsername { get; set; }

    /// <summary>MQTT 密码</summary>
    public string? MqttPassword { get; set; }

    /// <summary>MQTT QoS (0,1,2)</summary>
    public int? MqttQos { get; set; }

    // HTTP 配置
    /// <summary>HTTP 方法 (GET/POST)</summary>
    public string? HttpMethod { get; set; }

    /// <summary>HTTP 认证 Token</summary>
    public string? HttpToken { get; set; }

    /// <summary>HTTP 超时时间 (毫秒)</summary>
    public int? HttpTimeout { get; set; }

    /// <summary>HTTP 模式 (client/server)</summary>
    public string? HttpMode { get; set; }

    // WebSocket 配置
    /// <summary>WebSocket 订阅主题</summary>
    public string? WsSubscribeTopic { get; set; }

    /// <summary>WebSocket 心跳间隔 (毫秒)</summary>
    public int? WsHeartbeatInterval { get; set; }

    // 本地文件配置
    /// <summary>文件格式 (json/csv)</summary>
    public string? FileFormat { get; set; }

    /// <summary>文件保存路径</summary>
    public string? FilePath { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>已绑定的数据点数量</summary>
    public int MappedDataPointCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
