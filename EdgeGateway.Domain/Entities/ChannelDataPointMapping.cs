namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 通道与数据点的映射关系
/// 通过此表建立"哪些数据点的数据需要通过哪个通道发出去"的关联
/// 一个数据点可以映射到多个通道，一个通道也可以包含多个数据点（多对多）
/// 支持普通数据点和虚拟数据点
/// </summary>
public class ChannelDataPointMapping
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>发送通道 ID（外键）</summary>
    public int ChannelId { get; set; }

    /// <summary>数据点 ID（外键，普通数据点）</summary>
    public int? DataPointId { get; set; }

    /// <summary>虚拟数据点 ID（外键，虚拟数据点）</summary>
    public int? VirtualDataPointId { get; set; }

    /// <summary>发送时是否启用此映射</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>通道导航属性</summary>
    public Channel? Channel { get; set; }

    /// <summary>数据点导航属性（普通数据点）</summary>
    public DataPoint? DataPoint { get; set; }

    /// <summary>虚拟数据点导航属性</summary>
    public VirtualDataPoint? VirtualDataPoint { get; set; }
}
