using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 发送数据包
/// 封装了本次需要发送的采集数据及目标通道配置信息
/// 由发送服务（DataSendService）构建后传递给具体发送策略
/// </summary>
public class SendPackage
{
    /// <summary>目标发送通道配置</summary>
    public Channel Channel { get; set; } = null!;

    /// <summary>本次需要通过该通道发送的采集数据列表</summary>
    public IEnumerable<CollectedData> DataList { get; set; } = Enumerable.Empty<CollectedData>();

    /// <summary>
    /// 数据点与通道的映射配置（含别名等信息）
    /// 发送策略通过映射表将数据点Tag转换为目标系统要求的字段名
    /// </summary>
    public IEnumerable<ChannelDataPointMapping> Mappings { get; set; } = Enumerable.Empty<ChannelDataPointMapping>();
}
