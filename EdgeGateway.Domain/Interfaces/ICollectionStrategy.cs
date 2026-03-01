using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 【策略接口】设备数据采集策略接口
/// 每种通信协议（Modbus、OPC UA、S7等）实现此接口，提供统一的采集入口
/// 通过策略注册器根据设备协议类型动态选取具体实现
/// </summary>
public interface ICollectionStrategy
{
    /// <summary>
    /// 该策略对应的协议名称标识
    /// 工厂/注册器通过此属性匹配对应的策略实现类
    /// </summary>
    string ProtocolName { get; }

    /// <summary>是否已成功建立连接</summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接设备（建立通信链路）
    /// </summary>
    /// <param name="device">目标设备配置信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ConnectAsync(Device device, CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开设备连接，释放通信资源
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量读取数据点的当前值
    /// </summary>
    /// <param name="dataPoints">需要采集的数据点列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>采集结果列表，每个数据点对应一条 CollectedData</returns>
    Task<IEnumerable<CollectedData>> ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        CancellationToken cancellationToken = default);
}
