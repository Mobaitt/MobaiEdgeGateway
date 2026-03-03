namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>通道 - 数据点映射关系详情响应</summary>
public class MappingResponse
{
    public int Id { get; set; }
    public int ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public int? DataPointId { get; set; }
    public int? VirtualDataPointId { get; set; }

    /// <summary>数据点 Tag（如 "Device01.Temperature"）</summary>
    public string DataPointTag { get; set; } = string.Empty;

    public string DataPointName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>是否为虚拟数据点</summary>
    public bool IsVirtual { get; set; }
}
