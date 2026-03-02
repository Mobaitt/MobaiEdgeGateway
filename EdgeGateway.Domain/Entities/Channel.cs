using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 发送通道实体 - 代表一条数据上传通道（MQTT Broker、HTTP 接口、Kafka 等）
/// 采集的数据点通过与通道建立映射关系，决定往哪条通道发送数据
/// </summary>
public class Channel
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>通道名称（如 "云端 MQTT"、"本地 HTTP 接口"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>通道编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>通道描述</summary>
    public string? Description { get; set; }

    /// <summary>发送协议类型（策略模式的类型标识）</summary>
    public SendProtocol Protocol { get; set; }

    /// <summary>连接字符串或 Endpoint（如 "mqtt://broker.example.com:1883"）</summary>
    public string Endpoint { get; set; } = string.Empty;

    // MQTT 配置字段
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

    // HTTP 配置字段
    /// <summary>HTTP 方法 (GET/POST)</summary>
    public string? HttpMethod { get; set; } = "POST";

    /// <summary>HTTP 认证 Token</summary>
    public string? HttpToken { get; set; }

    /// <summary>HTTP 超时时间 (毫秒)</summary>
    public int? HttpTimeout { get; set; } = 5000;

    /// <summary>HTTP 模式 (client/server)</summary>
    public string? HttpMode { get; set; } = "client";

    // WebSocket 配置字段
    /// <summary>WebSocket 订阅主题</summary>
    public string? WsSubscribeTopic { get; set; }

    /// <summary>WebSocket 心跳间隔 (毫秒)</summary>
    public int? WsHeartbeatInterval { get; set; } = 30000;

    // 本地文件配置字段
    /// <summary>文件格式 (json/csv)</summary>
    public string? FileFormat { get; set; } = "json";

    /// <summary>文件保存路径</summary>
    public string? FilePath { get; set; } = "./output/data.json";

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>该通道绑定的数据点映射列表</summary>
    public ICollection<ChannelDataPointMapping> DataPointMappings { get; set; } = new List<ChannelDataPointMapping>();
}
