using EdgeGateway.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>创建发送通道请求</summary>
public class CreateChannelRequest
{
    /// <summary>通道名称（如 "云端MQTT"）</summary>
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

    /// <summary>
    /// 额外配置JSON（可选）
    /// MQTT示例：{ "topic": "edge/data", "clientId": "gw01" }
    /// HTTP示例：{ "token": "Bearer xxx", "timeout": 5000 }
    /// </summary>
    public string? ConfigJson { get; set; }

    /// <summary>是否启用（默认启用）</summary>
    public bool IsEnabled { get; set; } = true;
}
