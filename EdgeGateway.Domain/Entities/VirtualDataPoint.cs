using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 虚拟数据点实体 - 通过表达式计算得出的数据点
/// 依赖于其他真实数据点或其他虚拟数据点
/// 虚拟节点依附于普通设备，可以像普通数据点一样被管理和发送
/// </summary>
public class VirtualDataPoint
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>所属设备 ID（外键）</summary>
    public int DeviceId { get; set; }

    /// <summary>虚拟数据点名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>虚拟数据点标签（全局唯一，如 "DEV001.Virtual.TempAvg"）</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>虚拟数据点描述</summary>
    public string? Description { get; set; }

    /// <summary>计算表达式（如 "Point1 + Point2" 或 "Avg(Temp1, Temp2, Temp3)"）</summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>计算类型</summary>
    public CalculationType CalculationType { get; set; } = CalculationType.Custom;

    /// <summary>计算结果数据类型</summary>
    public DataValueType DataType { get; set; } = DataValueType.Float;

    /// <summary>工程量单位</summary>
    public string? Unit { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>表达式中依赖的数据点 Tags（JSON 数组格式，用于快速查找依赖）</summary>
    public string DependencyTags { get; set; } = "[]";

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属设备（导航属性）</summary>
    public Device? Device { get; set; }

    /// <summary>该虚拟数据点与发送通道的映射关系</summary>
    public ICollection<ChannelDataPointMapping> ChannelMappings { get; set; } = new List<ChannelDataPointMapping>();
}
