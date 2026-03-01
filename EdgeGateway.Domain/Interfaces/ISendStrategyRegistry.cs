using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 发送策略注册器接口
/// </summary>
public interface ISendStrategyRegistry
{
    /// <summary>
    /// 注册发送策略
    /// </summary>
    ISendStrategyRegistry Register<TStrategy>(SendProtocol protocol) where TStrategy : ISendStrategy;
    
    /// <summary>
    /// 根据协议解析对应的发送策略实例
    /// </summary>
    ISendStrategy Resolve(SendProtocol protocol);
}
