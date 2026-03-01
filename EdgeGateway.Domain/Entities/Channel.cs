using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 发送通道实体 - 代表一条数据上传通道（MQTT Broker、HTTP接口、Kafka等）
/// 采集的数据点通过与通道建立映射关系，决定往哪条通道发送数据
/// </summary>
public class Channel
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>通道名称（如 "云端MQTT"、"本地HTTP接口"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>通道编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>通道描述</summary>
    public string? Description { get; set; }

    /// <summary>发送协议类型（策略模式的类型标识）</summary>
    public SendProtocol Protocol { get; set; }

    /// <summary>连接字符串或Endpoint（如 "mqtt://broker.example.com:1883"）</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>额外配置JSON（主题、认证信息等，根据不同协议存不同内容）</summary>
    public string? ConfigJson { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>该通道绑定的数据点映射列表</summary>
    public ICollection<ChannelDataPointMapping> DataPointMappings { get; set; } = new List<ChannelDataPointMapping>();
}
