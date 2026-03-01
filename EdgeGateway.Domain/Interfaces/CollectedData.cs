namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 单个数据点的采集结果
/// 由采集策略读取后封装返回，传递给发送服务处理
/// </summary>
public class CollectedData
{
    /// <summary>数据点Tag（全局唯一标识，如 "Device01.Temperature"）</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>数据点ID（对应 DataPoint.Id）</summary>
    public int DataPointId { get; set; }

    /// <summary>所属设备ID</summary>
    public int DeviceId { get; set; }

    /// <summary>所属设备名称</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>采集到的值（统一用object存储，后续按DataType转换）</summary>
    public object? Value { get; set; }

    /// <summary>采集质量（Good/Bad/Uncertain）</summary>
    public DataQuality Quality { get; set; } = DataQuality.Good;

    /// <summary>采集时间戳（UTC）</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
