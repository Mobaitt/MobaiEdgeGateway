using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 采集策略注册器（策略工厂）
/// 在程序启动时将协议枚举与对应的策略实现类型绑定
/// 运行时根据设备协议类型动态从 DI 容器解析策略实例
/// </summary>
public class CollectionStrategyRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CollectionStrategyRegistry> _logger;

    /// <summary>协议枚举 → 策略实现类型的映射表</summary>
    private readonly Dictionary<CollectionProtocol, Type> _strategyMap = new();

    public CollectionStrategyRegistry(
        IServiceProvider serviceProvider,
        ILogger<CollectionStrategyRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    /// <summary>
    /// 注册采集策略（启动时调用，支持链式调用）
    /// </summary>
    /// <typeparam name="TStrategy">策略实现类型，须实现 ICollectionStrategy</typeparam>
    /// <param name="protocol">对应的采集协议枚举值</param>
    public CollectionStrategyRegistry Register<TStrategy>(CollectionProtocol protocol)
        where TStrategy : ICollectionStrategy
    {
        _strategyMap[protocol] = typeof(TStrategy);
        _logger.LogInformation("采集策略注册: {Protocol} -> {Strategy}", protocol, typeof(TStrategy).Name);
        return this;
    }

    /// <summary>
    /// 根据协议类型解析对应的采集策略实例
    /// 每次调用从 DI 容器获取新实例（Transient），避免跨设备状态污染
    /// </summary>
    /// <param name="protocol">设备的采集协议类型</param>
    /// <exception cref="NotSupportedException">协议未注册时抛出</exception>
    public ICollectionStrategy Resolve(CollectionProtocol protocol)
    {
        if (!_strategyMap.TryGetValue(protocol, out var strategyType))
            throw new NotSupportedException(
                $"不支持的采集协议: {protocol}，请在启动时通过 Register<T>() 注册对应的策略实现");

        return (ICollectionStrategy)_serviceProvider.GetRequiredService(strategyType);
    }
}
