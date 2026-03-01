namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>通道-数据点映射关系详情响应</summary>
public class MappingResponse
{
    public int Id { get; set; }
    public int ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public int DataPointId { get; set; }

    /// <summary>数据点Tag（如 "Device01.Temperature"）</summary>
    public string DataPointTag { get; set; } = string.Empty;

    public string DataPointName { get; set; } = string.Empty;

    /// <summary>
    /// 发送时的字段别名（可选）
    /// 不为空时，发送策略将使用此名称替代 Tag 作为字段名
    /// </summary>
    public string? AliasName { get; set; }

    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}
