using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 发送策略注册器（策略工厂）
/// 在程序启动时将发送协议枚举与对应的策略实现类型绑定
/// 运行时根据通道发送协议类型动态从 DI 容器解析策略实例
/// </summary>
public class SendStrategyRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SendStrategyRegistry> _logger;

    /// <summary>发送协议枚举 → 策略实现类型的映射表</summary>
    private readonly Dictionary<SendProtocol, Type> _strategyMap = new();

    public SendStrategyRegistry(
        IServiceProvider serviceProvider,
        ILogger<SendStrategyRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    /// <summary>
    /// 注册发送策略（启动时调用，支持链式调用）
    /// </summary>
    /// <typeparam name="TStrategy">策略实现类型，须实现 ISendStrategy</typeparam>
    /// <param name="protocol">对应的发送协议枚举值</param>
    public SendStrategyRegistry Register<TStrategy>(SendProtocol protocol)
        where TStrategy : ISendStrategy
    {
        _strategyMap[protocol] = typeof(TStrategy);
        _logger.LogInformation("发送策略注册: {Protocol} -> {Strategy}", protocol, typeof(TStrategy).Name);
        return this;
    }

    /// <summary>
    /// 根据通道发送协议类型解析对应的发送策略实例
    /// </summary>
    /// <param name="protocol">通道的发送协议类型</param>
    /// <exception cref="NotSupportedException">协议未注册时抛出</exception>
    public ISendStrategy Resolve(SendProtocol protocol)
    {
        if (!_strategyMap.TryGetValue(protocol, out var strategyType))
            throw new NotSupportedException(
                $"不支持的发送协议: {protocol}，请在启动时通过 Register<T>() 注册对应的策略实现");

        return (ISendStrategy)_serviceProvider.GetRequiredService(strategyType);
    }
}
