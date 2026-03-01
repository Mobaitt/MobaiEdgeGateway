namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 通道与数据点的映射关系
/// 通过此表建立"哪些数据点的数据需要通过哪个通道发出去"的关联
/// 一个数据点可以映射到多个通道，一个通道也可以包含多个数据点（多对多）
/// </summary>
public class ChannelDataPointMapping
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>发送通道ID（外键）</summary>
    public int ChannelId { get; set; }

    /// <summary>数据点ID（外键）</summary>
    public int DataPointId { get; set; }

    /// <summary>发送时是否启用此映射</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 发送时的字段别名（可选）
    /// 例如：数据点Tag为 "Device01.Temp"，但发到某个系统时需要叫 "temperature"
    /// </summary>
    public string? AliasName { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>通道导航属性</summary>
    public Channel? Channel { get; set; }

    /// <summary>数据点导航属性</summary>
    public DataPoint? DataPoint { get; set; }
}
