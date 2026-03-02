using EdgeGateway.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>创建发送通道请求</summary>
public class CreateChannelRequest
{
    /// <summary>通道名称（如 "云端 MQTT"）</summary>
    [Required(ErrorMessage = "通道名称不能为空")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>通道编码（全局唯一）</summary>
    [Required(ErrorMessage = "通道编码不能为空")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>描述（可选）</summary>
    public string? Description { get; set; }

    /// <summary>发送协议（1=Mqtt 2=Http 3=Kafka 4=LocalFile 5=WebSocket）</summary>
    [Required(ErrorMessage = "发送协议不能为空")]
    public SendProtocol Protocol { get; set; }

    /// <summary>
    /// 连接地址（Endpoint）
    /// 示例：mqtt://broker.example.com:1883 | https://api.example.com/upload | ./output/data.json
    /// </summary>
    [Required(ErrorMessage = "连接地址不能为空")]
    [MaxLength(500)]
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
    public string? HttpMethod { get; set; } = "POST";

    /// <summary>HTTP 认证 Token</summary>
    public string? HttpToken { get; set; }

    /// <summary>HTTP 超时时间 (毫秒)</summary>
    public int? HttpTimeout { get; set; } = 5000;

    /// <summary>HTTP 模式 (client/server)</summary>
    public string? HttpMode { get; set; } = "client";

    // WebSocket 配置
    /// <summary>WebSocket 订阅主题</summary>
    public string? WsSubscribeTopic { get; set; }

    /// <summary>WebSocket 心跳间隔 (毫秒)</summary>
    public int? WsHeartbeatInterval { get; set; } = 30000;

    // 本地文件配置
    /// <summary>文件格式 (json/csv)</summary>
    public string? FileFormat { get; set; } = "json";

    /// <summary>文件保存路径</summary>
    public string? FilePath { get; set; } = "./output/data.json";

    /// <summary>是否启用（默认启用）</summary>
    public bool IsEnabled { get; set; } = true;
}
