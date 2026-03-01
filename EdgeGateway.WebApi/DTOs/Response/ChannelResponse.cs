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

    /// <summary>额外配置JSON（主题、认证信息等）</summary>
    public string? ConfigJson { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>已绑定的数据点数量</summary>
    public int MappedDataPointCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
