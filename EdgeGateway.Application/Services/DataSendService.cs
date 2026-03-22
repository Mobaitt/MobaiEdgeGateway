using System.Collections.Concurrent;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Domain.Options;
using EdgeGateway.Infrastructure.Strategies.Send;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 数据发送服务 - 负责将采集数据按通道映射分发给各发送策略
///
/// 工作流程：
/// 1. 采集服务推送采集数据
/// 2. 从缓存加载所有启用的发送通道（含数据点映射关系）
/// 3. 遍历通道：筛选出该通道绑定的数据点数据
/// 4. 通过策略注册器获取对应的发送策略实例，执行发送
/// </summary>
public class DataSendService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SendStrategyRegistry _strategyRegistry;
    private readonly ILogger<DataSendService> _logger;
    private readonly GatewayOptions _options;

    // 已初始化的发送策略实例缓存（通道 ID → 策略实例），避免重复连接
    private readonly ConcurrentDictionary<int, ISendStrategy> _strategyCache = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // 通道配置缓存（通道 ID → 通道配置），避免频繁查询数据库
    private List<Channel> _cachedChannels = new();
    private DateTime _cacheUpdateTime = DateTime.MinValue;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly TimeSpan _cacheExpiration;

    public DataSendService(
        IServiceProvider serviceProvider,
        SendStrategyRegistry strategyRegistry,
        ILogger<DataSendService> logger,
        IOptions<GatewayOptions> options)
    {
        _serviceProvider  = serviceProvider;
        _strategyRegistry = strategyRegistry;
        _logger           = logger;
        _options          = options.Value;

        // 从配置读取发送相关参数
        _cacheExpiration = TimeSpan.FromSeconds(_options.Send.ChannelCacheExpirationSeconds);
    }

    /// <summary>
    /// 启用发送通道（动态加载并初始化）
    /// </summary>
    public async Task EnableChannelAsync(int channelId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();

        var channel = await channelRepo.GetByIdAsync(channelId);
        if (channel == null)
        {
            _logger.LogWarning("启用通道失败：通道 ID={ChannelId} 不存在", channelId);
            return;
        }

        // 如果通道已在缓存中，重新初始化（用于 HTTP 服务端模式重新注册端点）
        if (_strategyCache.TryGetValue(channelId, out var existingStrategy))
        {
            _logger.LogInformation("通道 [{ChannelName}] 已在启用状态，重新初始化", channel.Name);
            await existingStrategy.InitializeAsync(channel, cancellationToken);
            return;
        }

        try
        {
            var strategy = _strategyRegistry.Resolve(channel.Protocol);
            await strategy.InitializeAsync(channel, cancellationToken);
            _strategyCache[channelId] = strategy;
            _logger.LogInformation("发送通道已启用：{ChannelName} (ID={Id})", channel.Name, channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启用通道 [{ChannelName}] 失败", channel.Name);
        }
    }

    /// <summary>
    /// 停用发送通道（断开连接并从缓存移除）
    /// </summary>
    public async Task DisableChannelAsync(int channelId)
    {
        if (_strategyCache.TryRemove(channelId, out var strategy))
        {
            try
            {
                await strategy.DisposeAsync();
                _logger.LogInformation("发送通道已停用：ID={ChannelId}", channelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停用通道 [{ChannelId}] 时释放资源失败", channelId);
            }
        }
    }

    /// <summary>
    /// 初始化所有启用通道的发送策略（程序启动时调用一次）
    /// </summary>
    public async Task InitializeChannelsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();

        var channels = (await channelRepo.GetEnabledAsync()).ToList();
        _logger.LogInformation("正在初始化 {Count} 个发送通道", channels.Count);

        foreach (var channel in channels)
        {
            try
            {
                var strategy = _strategyRegistry.Resolve(channel.Protocol);
                await strategy.InitializeAsync(channel, cancellationToken);
                _strategyCache[channel.Id] = strategy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化通道 [{ChannelName}] 失败", channel.Name);
            }
        }

        // 初始化通道缓存
        await RefreshChannelsCacheAsync();

        _logger.LogInformation("发送通道初始化完成，成功：{Count}/{Total}",
            _strategyCache.Count, channels.Count);
    }

    /// <summary>
    /// 获取通道配置缓存（缓存过期时自动刷新）
    /// </summary>
    private async Task<List<Channel>> GetCachedChannelsAsync()
    {
        // 检查缓存是否过期
        if (DateTime.UtcNow - _cacheUpdateTime < _cacheExpiration)
        {
            return _cachedChannels;
        }

        // 缓存过期，刷新缓存
        await RefreshChannelsCacheAsync();
        return _cachedChannels;
    }

    /// <summary>
    /// 刷新通道配置缓存
    /// </summary>
    private async Task RefreshChannelsCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            // 双重检查，避免重复刷新
            if (DateTime.UtcNow - _cacheUpdateTime < _cacheExpiration)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();

            _cachedChannels = (await channelRepo.GetEnabledWithMappingsAsync()).ToList();
            _cacheUpdateTime = DateTime.UtcNow;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 强制刷新通道配置缓存（用于通道配置变更时）
    /// </summary>
    public async Task RefreshChannelsCacheForceAsync()
    {
        _cacheUpdateTime = DateTime.MinValue; // 强制过期
        await RefreshChannelsCacheAsync();
    }

    /// <summary>
    /// 将采集数据分发到各个绑定的通道进行发送
    ///
    /// 分发逻辑：遍历所有启用通道 → 筛选该通道绑定的数据点 → 调用对应发送策略
    /// 对于快照中不存在的数据点，使用 null 占位，确保数据结构完整
    /// </summary>
    /// <param name="collectedData">本次采集的数据列表（全量数据快照）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DispatchAsync(
        IEnumerable<CollectedData> collectedData,
        CancellationToken cancellationToken = default)
    {
        // 从缓存加载通道配置（缓存过期时自动刷新）
        var channels = await GetCachedChannelsAsync();

        // 将采集数据按 DataPointId 建立索引（全量快照）
        var dataIndex = collectedData.ToDictionary(d => d.DataPointId);

        // 并行向各通道发送（各通道发送互不阻塞）
        var sendTasks = channels.Select(async channel =>
        {
            try
            {
                // 筛选该通道绑定且启用的映射（包括普通数据点和虚拟数据点）
                var enabledDataMappings = channel.DataPointMappings
                    .Where(m => m.IsEnabled && m.DataPointId.HasValue)
                    .ToList();
                
                var enabledVirtualMappings = channel.VirtualDataPointMappings
                    .Where(m => m.IsEnabled && m.VirtualDataPointId.HasValue)
                    .ToList();

                // 根据映射关系，从快照中提取对应的数据点数据
                // 如果快照中不存在（设备离线/未采集），使用 null 占位
                var channelData = enabledDataMappings
                    .Select(m => dataIndex.TryGetValue(m.DataPointId!.Value, out var data) ? data : CreatePlaceholderData(m))
                    .ToList();
                
                // 添加虚拟数据点
                var virtualData = enabledVirtualMappings
                    .Select(m => dataIndex.TryGetValue(-m.VirtualDataPointId!.Value, out var data) ? data : CreateVirtualPlaceholderData(m))
                    .ToList();
                
                channelData.AddRange(virtualData);

                if (!channelData.Any())
                {
                    // 本次采集结果中没有该通道关心的数据点，跳过
                    return;
                }

                // 构建发送包
                var package = new SendPackage
                {
                    Channel  = channel,
                    DataList = channelData,
                    Mappings = enabledDataMappings.Concat(enabledVirtualMappings).ToList()
                };

                // 获取（或懒加载）该通道的发送策略
                var strategy = await GetOrCreateStrategyAsync(channel, cancellationToken);
                if (strategy == null) return;

                // 执行发送
                var result = await strategy.SendAsync(package, cancellationToken);

                if (!result.IsSuccess)
                    _logger.LogWarning("通道 [{ChannelName}] 发送失败：{Error}", channel.Name, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通道 [{ChannelName}] 发送异常", channel.Name);
            }
        });

        await Task.WhenAll(sendTasks);
    }

    /// <summary>
    /// 为缺失的数据点创建占位数据（用于保持数据结构完整）
    /// </summary>
    private static CollectedData CreatePlaceholderData(ChannelDataPointMapping mapping)
    {
        return new CollectedData
        {
            DataPointId = mapping.DataPointId ?? 0,
            Tag = mapping.DataPoint?.Tag ?? string.Empty,
            DeviceId = mapping.DataPoint?.DeviceId ?? 0,
            DeviceName = mapping.DataPoint?.Device?.Name ?? "Unknown",
            Value = null,
            Quality = DataQuality.Uncertain,
            Timestamp = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// 为缺失的虚拟数据点创建占位数据
    /// </summary>
    private static CollectedData CreateVirtualPlaceholderData(ChannelDataPointMapping mapping)
    {
        return new CollectedData
        {
            DataPointId = -mapping.VirtualDataPointId!.Value, // 虚拟数据点使用负 ID
            Tag = mapping.VirtualDataPoint?.Tag ?? string.Empty,
            DeviceId = mapping.VirtualDataPoint?.DeviceId ?? 0,
            DeviceName = mapping.VirtualDataPoint?.Device?.Name ?? "Unknown",
            Value = null,
            Quality = DataQuality.Uncertain,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 重新配置通道的端点路径（用于 HTTP 服务端模式路径更新）
    /// </summary>
    public async Task ReconfigureChannelEndpointAsync(int channelId, string? oldEndpoint)
    {
        if (!_strategyCache.TryGetValue(channelId, out var strategy))
            return;

        if (strategy is HttpSendStrategy httpStrategy)
        {
            using var scope = _serviceProvider.CreateScope();
            var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
            var channel = await channelRepo.GetByIdAsync(channelId);

            if (channel != null)
            {
                await httpStrategy.ReconfigureEndpointAsync(channel, oldEndpoint ?? string.Empty, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// 获取或懒加载创建通道对应的发送策略实例（线程安全）
    /// </summary>
    private async Task<ISendStrategy?> GetOrCreateStrategyAsync(
        Domain.Entities.Channel channel,
        CancellationToken cancellationToken)
    {
        if (_strategyCache.TryGetValue(channel.Id, out var cached))
            return cached;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查（防止并发重复初始化）
            if (_strategyCache.TryGetValue(channel.Id, out cached))
                return cached;

            var strategy = _strategyRegistry.Resolve(channel.Protocol);
            await strategy.InitializeAsync(channel, cancellationToken);
            _strategyCache[channel.Id] = strategy;
            return strategy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通道 [{ChannelName}] 策略懒加载失败", channel.Name);
            return null;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 释放所有发送策略资源（程序关闭时调用）
    /// </summary>
    public async Task DisposeAllAsync()
    {
        foreach (var (id, strategy) in _strategyCache.ToArray())
        {
            try { await strategy.DisposeAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "释放通道 [{Id}] 策略资源时出错", id); }
        }
        _strategyCache.Clear();
        _initLock.Dispose();
    }
}
