using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 设备实体 - 代表一个现场采集设备（如PLC、传感器等）
/// </summary>
public class Device
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>设备名称（唯一标识，如 "车间A-PLC01"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>设备编码（业务编码）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>设备描述</summary>
    public string? Description { get; set; }

    /// <summary>通信协议类型（Modbus、OpcUA 等）</summary>
    public CollectionProtocol Protocol { get; set; }

    /// <summary>设备IP地址或串口号</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>通信端口（TCP时使用）</summary>
    public int? Port { get; set; }

    /// <summary>设备是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>采集周期（毫秒）</summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>该设备下的数据点列表（导航属性）</summary>
    public ICollection<DataPoint> DataPoints { get; set; } = new List<DataPoint>();
}
