namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>设备列表项响应（精简版，用于列表展示，减少传输数据量）</summary>
public class DeviceListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>协议名称（如 "Modbus"、"Simulator"）</summary>
    public string Protocol { get; set; } = string.Empty;
    public int ProtocolValue { get; set; }
    
    /// <summary>采集周期（毫秒）</summary>
    public int PollingIntervalMs { get; set; } = 1000;

    public string Address { get; set; } = string.Empty;
    public int? Port { get; set; }
    public bool IsEnabled { get; set; }
    public int DataPointCount { get; set; }
}
