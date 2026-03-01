namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>设备详情响应（完整信息，用于详情页）</summary>
public class DeviceResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>协议名称（字符串，如 "Modbus"）</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>协议枚举值（数字，方便前端下拉回显）</summary>
    public int ProtocolValue { get; set; }

    public string Address { get; set; } = string.Empty;
    public int? Port { get; set; }
    public bool IsEnabled { get; set; }
    public int PollingIntervalMs { get; set; }
    public int DataPointCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
