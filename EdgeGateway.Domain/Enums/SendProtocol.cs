namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 发送协议枚举 - 决定使用哪种发送策略
/// </summary>
public enum SendProtocol
{
    /// <summary>MQTT消息队列</summary>
    Mqtt = 1,

    /// <summary>HTTP REST接口</summary>
    Http = 2,

    /// <summary>Kafka消息队列</summary>
    Kafka = 3,

    /// <summary>写入本地文件（CSV / JSON）</summary>
    LocalFile = 4,

    /// <summary>WebSocket推送</summary>
    WebSocket = 5
}
