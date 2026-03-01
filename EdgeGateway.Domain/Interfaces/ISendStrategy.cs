using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 【策略接口】数据发送策略接口
/// 每种发送方式（MQTT、HTTP、Kafka、本地文件等）实现此接口
/// 通过策略注册器根据通道的发送协议类型动态选取具体实现
/// </summary>
public interface ISendStrategy
{
    /// <summary>
    /// 该策略对应的发送协议名称标识
    /// 注册器通过此属性与 SendProtocol 枚举匹配
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// 初始化通道连接（如连接MQTT Broker、验证HTTP接口可达性等）
    /// 程序启动时或懒加载时调用一次
    /// </summary>
    /// <param name="channel">通道配置信息（含Endpoint、ConfigJson等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将数据包发送到目标通道
    /// </summary>
    /// <param name="package">待发送的数据包（含通道配置、数据列表、映射关系）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放通道连接资源（程序关闭时调用）
    /// </summary>
    Task DisposeAsync();
}
