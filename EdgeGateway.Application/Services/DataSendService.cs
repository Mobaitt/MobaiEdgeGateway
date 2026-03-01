using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Strategies.Send;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 数据发送服务 - 负责将采集数据按通道映射分发给各发送策略
///
/// 工作流程：
/// 1. 采集服务推送采集数据
/// 2. 从数据库加载所有启用的发送通道（含数据点映射关系）
/// 3. 遍历通道：筛选出该通道绑定的数据点数据
/// 4. 通过策略注册器获取对应的发送策略实例，执行发送
/// </summary>
public class DataSendService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SendStrategyRegistry _strategyRegistry;
    private readonly ILogger<DataSendService> _logger;

    // 已初始化的发送策略实例缓存（通道 ID → 策略实例），避免重复连接
    private readonly Dictionary<int, ISendStrategy> _strategyCache = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public DataSendService(
        IServiceProvider serviceProvider,
        SendStrategyRegistry strategyRegistry,
        ILogger<DataSendService> logger)
    {
        _serviceProvider  = serviceProvider;
        _strategyRegistry = strategyRegistry;
        _logger           = logger;
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

        if (_strategyCache.ContainsKey(channelId))
        {
            _logger.LogInformation("通道 [{ChannelName}] 已在启用状态", channel.Name);
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
        if (_strategyCache.TryGetValue(channelId, out var strategy))
        {
            try
            {
                await strategy.DisposeAsync();
                _strategyCache.Remove(channelId);
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
                _logger.LogError(ex, "通道 [{ChannelName}] 初始化失败，该通道将跳过", channel.Name);
            }
        }

        _logger.LogInformation("发送通道初始化完成，成功：{Count}/{Total}",
            _strategyCache.Count, channels.Count);
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
        // 每次调用创建新的 Scope，获取独立的 DbContext 实例
        using var scope = _serviceProvider.CreateScope();
        var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();

        // 加载通道（含数据点映射详情）
        var channels = (await channelRepo.GetEnabledWithMappingsAsync()).ToList();

        // 将采集数据按 DataPointId 建立索引（全量快照）
        var dataIndex = collectedData.ToDictionary(d => d.DataPointId);

        // 并行向各通道发送（各通道发送互不阻塞）
        var sendTasks = channels.Select(async channel =>
        {
            try
            {
                // 筛选该通道绑定且启用的映射
                var enabledMappings = channel.DataPointMappings
                    .Where(m => m.IsEnabled)
                    .ToList();

                // 根据映射关系，从快照中提取对应的数据点数据
                // 如果快照中不存在（设备离线/未采集），使用 null 占位
                var channelData = enabledMappings
                    .Select(m => dataIndex.TryGetValue(m.DataPointId, out var data) ? data : CreatePlaceholderData(m))
                    .ToList();

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
                    Mappings = enabledMappings
                };

                // 获取（或懒加载）该通道的发送策略
                var strategy = await GetOrCreateStrategyAsync(channel, cancellationToken);
                if (strategy == null) return;

                // 执行发送
                var result = await strategy.SendAsync(package, cancellationToken);

                if (result.IsSuccess)
                    _logger.LogDebug("通道 [{ChannelName}] 发送成功，条数：{Count}", channel.Name, result.SentCount);
                else
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
            DataPointId = mapping.DataPointId,
            Tag = mapping.DataPoint?.Tag ?? string.Empty,
            DeviceId = mapping.DataPoint?.DeviceId ?? 0,
            DeviceName = mapping.DataPoint?.Device?.Name ?? "Unknown",
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
        _logger.LogInformation("重新配置通道端点：ChannelId={ChannelId}, OldEndpoint={OldEndpoint}", channelId, oldEndpoint);
        
        if (!_strategyCache.TryGetValue(channelId, out var strategy))
        {
            _logger.LogWarning("通道策略不在缓存中：ChannelId={ChannelId}", channelId);
            return;
        }

        _logger.LogInformation("通道策略类型：{StrategyType}", strategy.GetType().Name);

        if (strategy is HttpSendStrategy httpStrategy)
        {
            using var scope = _serviceProvider.CreateScope();
            var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
            var channel = await channelRepo.GetByIdAsync(channelId);

            if (channel != null)
            {
                _logger.LogInformation("调用 HTTP 策略重新配置：Channel={ChannelName}, Endpoint={Endpoint}", channel.Name, channel.Endpoint);
                await httpStrategy.ReconfigureEndpointAsync(channel, oldEndpoint ?? string.Empty, CancellationToken.None);
            }
            else
            {
                _logger.LogWarning("通道不存在：ChannelId={ChannelId}", channelId);
            }
        }
        else
        {
            _logger.LogWarning("策略不是 HTTP 类型：{StrategyType}", strategy.GetType().Name);
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
        foreach (var (id, strategy) in _strategyCache)
        {
            try { await strategy.DisposeAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "释放通道 [{Id}] 策略资源时出错", id); }
        }
        _strategyCache.Clear();
        _initLock.Dispose();
    }
}
