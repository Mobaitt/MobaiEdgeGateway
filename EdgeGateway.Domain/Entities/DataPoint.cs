using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 数据点实体 - 代表设备上的一个采集点（如寄存器地址、OPC节点等）
/// </summary>
public class DataPoint
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>所属设备ID（外键）</summary>
    public int DeviceId { get; set; }

    /// <summary>数据点名称（如 "温度"、"压力"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>数据点标签（唯一Tag，如 "Device01.Temperature"）</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>数据点描述</summary>
    public string? Description { get; set; }

    /// <summary>数据地址（Modbus寄存器地址 或 OPC节点ID）</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>数据类型（Int16、Float、Bool等）</summary>
    public DataValueType DataType { get; set; }

    /// <summary>工程量单位（如 "℃"、"MPa"）</summary>
    public string? Unit { get; set; }

    /// <summary>是否启用采集</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属设备（导航属性）</summary>
    public Device? Device { get; set; }

    /// <summary>该数据点与发送通道的映射关系</summary>
    public ICollection<ChannelDataPointMapping> ChannelMappings { get; set; } = new List<ChannelDataPointMapping>();
}
