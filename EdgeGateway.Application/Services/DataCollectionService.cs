using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 采集服务 - 负责调度所有设备的采集任务
///
/// 工作流程：
/// 1. 从数据库加载所有启用的设备（含数据点）
/// 2. 根据设备协议类型，通过注册器获取对应采集策略
/// 3. 建立连接，按设备采集周期定时读取数据
/// 4. 将采集结果更新到全量快照，按聚合窗口批量推送给发送服务
///
/// 数据聚合机制：
/// - 维护所有数据点的全量最新值快照（带时间戳）
/// - 按聚合窗口间隔（默认 1 秒）批量推送全量数据
/// - 数据点超过 1 分钟未更新则自动置为 null（Uncertain 状态）
/// - 确保同一通道绑定的所有设备数据始终完整发送，不会缺失
///
/// 缓存架构：
/// - _dataSnapshot: 全量数据快照（DataPointId → TimestampedData），唯一数据源
/// - _tagIndex: Tag 索引（Tag → DataPointId），支持按 Tag 快速查询
/// - _virtualDataPointCache: 虚拟数据点缓存（Tag → VirtualDataPoint），避免频繁 DB 查询
/// </summary>
public class DataCollectionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CollectionStrategyRegistry _strategyRegistry;
    private readonly DataSendService _sendService;
    private readonly IRuleEngine _ruleEngine;
    private readonly IVirtualNodeEngine _virtualNodeEngine;
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly ILogger<DataCollectionService> _logger;

    // 每个设备的采集任务句柄（设备 Id → CancellationTokenSource）
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _deviceTasks = new();

    // 全量数据快照：DataPointId → 带时间戳的采集数据（唯一数据源）
    // 使用 ConcurrentDictionary 替代 ReaderWriterLockSlim + Dictionary，提升并发性能
    private readonly ConcurrentDictionary<int, TimestampedData> _dataSnapshot = new();

    // Tag 索引：Tag → DataPointId，支持按 Tag 快速查询
    private readonly ConcurrentDictionary<string, int> _tagIndex = new();

    // 虚拟数据点缓存：Tag → VirtualDataPoint，避免频繁数据库查询
    // 注意：虚拟数据点缓存是全局的，因为 Tag 是全局唯一的
    private readonly ConcurrentDictionary<string, VirtualDataPoint> _virtualDataPointCache = new();

    // 设备 ID → 虚拟数据点 ID 列表，用于快速查找设备下的虚拟数据点
    private readonly ConcurrentDictionary<int, HashSet<int>> _deviceVirtualPointIds = new();

    private readonly int _aggregateWindowMs = 1000; // 聚合窗口：1 秒
    private readonly TimeSpan _dataExpiration = TimeSpan.FromSeconds(30); // 数据过期时间：30 秒
    private CancellationTokenSource? _aggregatorCts;
    private Task? _aggregatorTask;

    /// <summary>
    /// 带时间戳的数据包装（支持缓存过期机制）
    /// </summary>
    private class TimestampedData
    {
        public CollectedData Data { get; set; } = null!;
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 检查数据是否已过期（超过 30 秒没有更新）
        /// </summary>
        public bool IsExpired(TimeSpan expiration)
        {
            return LastUpdateTime == DateTime.MinValue ||
                   (DateTime.UtcNow - LastUpdateTime) > expiration;
        }
    }

    public DataCollectionService(
        IServiceProvider serviceProvider,
        CollectionStrategyRegistry strategyRegistry,
        DataSendService sendService,
        IRuleEngine ruleEngine,
        IVirtualNodeEngine virtualNodeEngine,
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        ILogger<DataCollectionService> logger)
    {
        _serviceProvider     = serviceProvider;
        _strategyRegistry    = strategyRegistry;
        _sendService         = sendService;
        _ruleEngine          = ruleEngine;
        _virtualNodeEngine   = virtualNodeEngine;
        _dbContextFactory    = dbContextFactory;
        _logger              = logger;

        // 设置虚拟节点引擎的快照数据获取委托
        if (virtualNodeEngine is EdgeGateway.Infrastructure.VirtualNodes.VirtualNodeEngine engine)
        {
            engine.SetDataSnapshotGetter(GetSnapshotValue);
        }
    }

    /// <summary>
    /// 根据 Tag 获取快照数据值（供虚拟节点引擎使用）
    /// 通过 _tagIndex 快速定位 DataPointId，然后从 _dataSnapshot 查询
    /// 数据过期时间为 30 秒，过期数据返回 null
    /// 使用 ConcurrentDictionary，无需锁，支持高并发读取
    /// </summary>
    private object? GetSnapshotValue(string tag)
    {
        // 从快照中查询（通过 Tag 索引）
        if (_tagIndex.TryGetValue(tag, out var dataPointId))
        {
            if (_dataSnapshot.TryGetValue(dataPointId, out var snapshotData))
            {
                // 数据过期，返回 null
                if (snapshotData.IsExpired(_dataExpiration))
                {
                    return null;
                }

                return snapshotData.Data.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 启动数据聚合器（后台任务，按窗口间隔批量推送全量数据）
    /// </summary>
    public async Task StartAggregatorAsync(CancellationToken cancellationToken)
    {
        // 预先加载所有通道绑定的数据点，确保即使未采集过也会出现在快照中
        await InitializeSnapshotFromMappingsAsync();

        _aggregatorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _aggregatorTask = Task.Run(async () =>
        {
            try
            {
                while (!_aggregatorCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(_aggregateWindowMs, _aggregatorCts.Token);
                    await FlushSnapshotAsync(_aggregatorCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("数据聚合器已停止");
            }
        }, _aggregatorCts.Token);

        _logger.LogInformation("数据聚合器已启动，窗口间隔：{WindowMs}ms", _aggregateWindowMs);
    }

    /// <summary>
    /// 从数据库加载所有通道绑定的数据点，初始化快照（即使未采集过也会出现在快照中）
    /// 使用 ConcurrentDictionary，无需锁
    /// </summary>
    private async Task InitializeSnapshotFromMappingsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();

        var channels = await channelRepo.GetEnabledWithMappingsAsync();
        foreach (var channel in channels)
        {
            // 处理普通数据点映射
            foreach (var mapping in channel.DataPointMappings.Where(m => m.IsEnabled && m.DataPointId.HasValue))
            {
                var dataPointId = mapping.DataPointId!.Value;
                if (!_dataSnapshot.ContainsKey(dataPointId))
                {
                    // 创建初始占位数据
                    _dataSnapshot[dataPointId] = new TimestampedData
                    {
                        Data = new CollectedData
                        {
                            DataPointId = dataPointId,
                            Tag = mapping.DataPoint?.Tag ?? "Unknown",
                            DeviceId = mapping.DataPoint?.DeviceId ?? 0,
                            DeviceName = mapping.DataPoint?.Device?.Name ?? "Unknown",
                            Value = null,
                            Quality = DataQuality.Uncertain,
                            Timestamp = DateTime.UtcNow
                        },
                        LastUpdateTime = DateTime.MinValue // 使用 MinValue，这样会被视为过期
                    };
                }
            }

            // 处理虚拟数据点映射
            foreach (var mapping in channel.VirtualDataPointMappings.Where(m => m.IsEnabled && m.VirtualDataPointId.HasValue))
            {
                var virtualDataPointId = mapping.VirtualDataPointId!.Value;
                // 虚拟数据点使用负 ID 存储，以区分于普通数据点
                var snapshotKey = -virtualDataPointId;
                if (!_dataSnapshot.ContainsKey(snapshotKey))
                {
                    _dataSnapshot[snapshotKey] = new TimestampedData
                    {
                        Data = new CollectedData
                        {
                            DataPointId = snapshotKey,
                            Tag = mapping.VirtualDataPoint?.Tag ?? "Unknown",
                            DeviceId = mapping.VirtualDataPoint?.DeviceId ?? 0,
                            DeviceName = mapping.VirtualDataPoint?.Device?.Name ?? "Unknown",
                            Value = null,
                            Quality = DataQuality.Uncertain,
                            Timestamp = DateTime.UtcNow
                        },
                        LastUpdateTime = DateTime.MinValue
                    };
                }
            }
        }

        _logger.LogInformation("快照初始化完成，共 {Count} 个数据点", _dataSnapshot.Count);
    }

    /// <summary>
    /// 初始化虚拟节点计算（服务启动时调用，确保虚拟节点有初始值）
    /// </summary>
    private async Task InitializeVirtualNodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("开始初始化虚拟节点计算...");

            // 计算所有虚拟节点
            var results = await _virtualNodeEngine.CalculateAllAsync(cancellationToken);

            // 将计算结果更新到快照和实时数据服务
            await UpdateVirtualNodeResultsToSnapshotAsync(results, cancellationToken);

            _logger.LogInformation("虚拟节点初始化完成：{SuccessCount}/{TotalCount} 成功",
                results.Count(r => r.Success), results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟节点初始化计算失败");
        }
    }

    /// <summary>
    /// 将全量快照数据批量推送给发送服务（超过 30 秒未更新的数据置为 null）
    /// 使用 ConcurrentDictionary，无需锁，支持读取期间并发写入
    /// </summary>
    private async Task FlushSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_dataSnapshot.IsEmpty)
                return;

            // 预分配列表容量，避免扩容开销
            var dataToSend = new List<CollectedData>(_dataSnapshot.Count);
            foreach (var kvp in _dataSnapshot)
            {
                // 使用 IsExpired 方法统一判断过期
                if (kvp.Value.IsExpired(_dataExpiration))
                {
                    // 超过 30 秒没有成功数据，创建 null 占位数据
                    dataToSend.Add(CreateTimeoutData(kvp.Value.Data));
                }
                else
                {
                    // 正常数据
                    dataToSend.Add(kvp.Value.Data);
                }
            }

            // 批量推送
            await _sendService.DispatchAsync(dataToSend, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "数据聚合推送失败");
        }
    }

    /// <summary>
    /// 为超时数据创建占位数据（Value=null, Quality=Uncertain）
    /// </summary>
    private static CollectedData CreateTimeoutData(CollectedData original)
    {
        return new CollectedData
        {
            DataPointId = original.DataPointId,
            Tag = original.Tag,
            DeviceId = original.DeviceId,
            DeviceName = original.DeviceName,
            Value = null,
            Quality = DataQuality.Uncertain,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 更新数据点到全量快照（线程安全）
    /// 质量为 Good 且 Value 不为 null 时，更新数据和 LastUpdateTime
    /// 质量为 Good 但 Value 为 null 时，保持上次成功值，不更新 LastUpdateTime（这样超过 30 秒会过期）
    /// 使用 ConcurrentDictionary，无需锁
    /// </summary>
    public Task UpdateSnapshotAsync(IEnumerable<CollectedData> data, CancellationToken cancellationToken)
    {
        foreach (var item in data)
        {
            // 只处理质量为 Good 的数据
            if (item.Quality != DataQuality.Good)
            {
                // 采集失败时，保持快照中的上次成功值，不更新
                continue;
            }

            // 维护 Tag 索引
            _tagIndex[item.Tag] = item.DataPointId;

            // 更新或添加数据点的最新值
            if (_dataSnapshot.TryGetValue(item.DataPointId, out var existing))
            {
                // 只有 Value 不为 null 时，才更新数据和 LastUpdateTime
                if (item.Value != null)
                {
                    existing.Data = item;
                    existing.LastUpdateTime = DateTime.UtcNow;
                }
                // 如果 Value 为 null，保持 existing.Data 和 LastUpdateTime 不变
                // 这样超过 30 秒后会自动过期
            }
            else
            {
                _dataSnapshot[item.DataPointId] = new TimestampedData
                {
                    Data = item,
                    LastUpdateTime = item.Value != null ? DateTime.UtcNow : DateTime.MinValue
                };
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取指定设备的所有数据点快照数据（用于 realtime 接口）
    /// 超过 30 秒未更新的数据返回 null（Uncertain）
    /// 使用 ConcurrentDictionary，无需锁，支持并发读取
    /// </summary>
    public List<CollectedData> GetDeviceSnapshotData(int deviceId)
    {
        var results = new List<CollectedData>();

        // 遍历快照，筛选出该设备的所有数据点（包括虚拟数据点）
        foreach (var kvp in _dataSnapshot)
        {
            if (kvp.Value.Data.DeviceId != deviceId)
                continue;

            // 使用 IsExpired 方法统一判断过期
            if (kvp.Value.IsExpired(_dataExpiration))
            {
                // 超过 30 秒没有成功数据，返回 null 占位数据
                results.Add(CreateTimeoutData(kvp.Value.Data));
            }
            else
            {
                // 正常数据
                results.Add(kvp.Value.Data);
            }
        }

        return results;
    }

    /// <summary>
    /// 启动所有启用设备的采集任务
    /// 每个设备独立运行一个后台采集循环（互不阻塞）
    /// </summary>
    public async Task StartAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

        var devices = (await deviceRepo.GetEnabledAsync()).ToList();
        _logger.LogInformation("共发现 {Count} 台启用设备，开始启动采集任务", devices.Count);

        // 启动数据聚合器
        await StartAggregatorAsync(cancellationToken);

        // 初始化虚拟节点计算（确保启动时虚拟节点有初始值）
        await InitializeVirtualNodesAsync(cancellationToken);

        foreach (var device in devices)
        {
            StartDeviceTask(device, cancellationToken);
        }
    }

    /// <summary>
    /// 为单个设备启动独立的采集循环
    /// </summary>
    private void StartDeviceTask(Device device, CancellationToken parentToken)
    {
        // 为每个设备创建独立的取消令牌，支持单独停止某个设备
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        _deviceTasks[device.Id] = cts;

        // 在后台线程运行采集循环（不 await，并发执行）
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("设备 [{DeviceName}] 采集任务启动，协议：{Protocol}，周期：{Interval}ms",
                device.Name, device.Protocol, device.PollingIntervalMs);

            // 从策略注册器获取对应的采集策略实例
            var strategy = _strategyRegistry.Resolve(device.Protocol);
            var enabledPoints = device.DataPoints.Where(dp => dp.IsEnabled).ToList();

            if (!enabledPoints.Any())
            {
                _logger.LogWarning("设备 [{DeviceName}] 没有启用的数据点，跳过采集", device.Name);
                return;
            }

            try
            {
                // 建立设备连接
                await strategy.ConnectAsync(device, cts.Token);

                // 采集循环
                while (!cts.Token.IsCancellationRequested)
                {
                    var cycleStart = DateTime.UtcNow;

                    try
                    {
                        // 执行一轮数据采集
                        var collectedData = (await strategy.ReadAsync(enabledPoints, cts.Token)).ToList();

                        // 执行规则引擎处理
                        var processedData = await ExecuteRuleEngineAsync(collectedData, cts.Token);

                        // 更新到全量快照（由聚合器按窗口间隔批量推送）
                        await UpdateSnapshotAsync(processedData, cts.Token);

                        // 触发虚拟节点计算
                        await ExecuteVirtualNodesAsync(processedData, cts.Token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // 采集异常不中断循环，记录日志后等待下一周期
                        _logger.LogError(ex, "设备 [{DeviceName}] 单次采集失败，等待下一周期重试", device.Name);
                    }

                    // 计算剩余等待时间（扣除采集耗时，尽量保证周期准确）
                    var elapsed = (DateTime.UtcNow - cycleStart).TotalMilliseconds;
                    var waitMs  = Math.Max(0, device.PollingIntervalMs - elapsed);
                    await Task.Delay((int)waitMs, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("设备 [{DeviceName}] 采集任务已取消", device.Name);
            }
            finally
            {
                // 断开连接，释放资源
                await strategy.DisconnectAsync();
                _logger.LogInformation("设备 [{DeviceName}] 连接已断开", device.Name);
            }
        }, cts.Token);
    }

    /// <summary>
    /// 停止指定设备的采集任务，并清理该设备的快照数据
    /// </summary>
    public void StopDevice(int deviceId)
    {
        if (_deviceTasks.TryRemove(deviceId, out var cts))
        {
            cts.Cancel();

            // 清理该设备的快照数据和 Tag 索引
            ClearDeviceSnapshotData(deviceId);

            _logger.LogInformation("设备 ID [{DeviceId}] 采集任务已停止", deviceId);
        }
    }

    /// <summary>
    /// 停止所有采集任务
    /// </summary>
    public void StopAll()
    {
        foreach (var cts in _deviceTasks.Values)
            cts.Cancel();
        _deviceTasks.Clear();
        _logger.LogInformation("所有采集任务已停止");
    }

    /// <summary>
    /// 重新加载指定设备的配置（用于数据点配置变更时实时更新）
    /// </summary>
    public async Task ReloadDeviceAsync(int deviceId, CancellationToken cancellationToken)
    {
        // 先停止原有采集任务
        StopDevice(deviceId);

        // 重新从数据库加载设备配置
        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepo.GetByIdAsync(deviceId);

        if (device == null || !device.IsEnabled)
        {
            _logger.LogInformation("设备 ID={DeviceId} 未启用或不存在，跳过重新加载", deviceId);
            return;
        }

        // 重新启动采集任务
        StartDeviceTask(device, cancellationToken);
        _logger.LogInformation("设备 ID={DeviceId} 配置已重新加载", deviceId);
    }

    /// <summary>
    /// 停止数据聚合器并刷新快照
    /// </summary>
    public async Task StopAggregatorAsync()
    {
        if (_aggregatorCts != null)
        {
            await _aggregatorCts.CancelAsync();
            _aggregatorCts.Dispose();

            if (_aggregatorTask != null)
            {
                try
                {
                    await _aggregatorTask;
                }
                catch (OperationCanceledException) { }
            }

            // 刷新剩余快照数据
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await FlushSnapshotAsync(cts.Token);

            _logger.LogInformation("数据聚合器已停止，快照已刷新");
        }
    }

    /// <summary>
    /// 根据设备 ID 启动采集任务（用于设备启用时）
    /// </summary>
    public async Task StartDeviceTaskByIdAsync(int deviceId)
    {
        // 如果任务已存在，先停止
        if (_deviceTasks.TryRemove(deviceId, out var existingCts))
        {
            await existingCts.CancelAsync();

            // 清理该设备的快照数据
            ClearDeviceSnapshotData(deviceId);

            _logger.LogInformation("设备 ID [{DeviceId}] 原有采集任务已停止，准备重启", deviceId);
        }

        // 创建 Scope 获取设备信息（避免 DbContext 并发问题）
        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

        // 从数据库加载设备（含数据点）
        var device = await deviceRepo.GetByIdAsync(deviceId);
        if (device == null || !device.IsEnabled)
        {
            _logger.LogWarning("设备 ID [{DeviceId}] 不存在或未启用，无法启动采集", deviceId);
            return;
        }

        // 使用全局 CancellationToken.None（由服务生命周期管理）
        StartDeviceTask(device, CancellationToken.None);

        // 设备启动后，触发该设备的虚拟节点计算
        await InitializeDeviceVirtualNodesAsync(deviceId, CancellationToken.None);
    }

    /// <summary>
    /// 初始化指定设备的虚拟节点计算（设备启动时调用）
    /// </summary>
    private async Task InitializeDeviceVirtualNodesAsync(int deviceId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("开始初始化设备 ID={DeviceId} 的虚拟节点计算...", deviceId);

            // 计算该设备的所有虚拟节点
            var results = await _virtualNodeEngine.CalculateDeviceAsync(deviceId, cancellationToken);

            // 将计算结果更新到快照和实时数据服务
            await UpdateVirtualNodeResultsToSnapshotAsync(results, cancellationToken);

            _logger.LogInformation("设备 ID={DeviceId} 虚拟节点初始化完成：{SuccessCount}/{TotalCount} 成功",
                deviceId, results.Count(r => r.Success), results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设备 ID={DeviceId} 虚拟节点初始化计算失败", deviceId);
        }
    }

    /// <summary>
    /// 执行规则引擎处理采集数据
    /// </summary>
    private async Task<List<CollectedData>> ExecuteRuleEngineAsync(List<CollectedData> collectedData, CancellationToken cancellationToken)
    {
        var processedData = new List<CollectedData>();

        foreach (var data in collectedData)
        {
            try
            {
                var result = await _ruleEngine.ExecuteRulesAsync(data, cancellationToken);

                if (!result.ShouldReject)
                {
                    // 创建处理后的数据
                    processedData.Add(new CollectedData
                    {
                        Tag = data.Tag,
                        DataPointId = data.DataPointId,
                        DeviceId = data.DeviceId,
                        DeviceName = data.DeviceName,
                        Value = result.Value,
                        Unit = data.Unit,
                        Quality = result.Quality,
                        Timestamp = data.Timestamp
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "规则执行失败，数据点：{Tag}", data.Tag);
                // 规则执行失败时保留原数据
                processedData.Add(data);
            }
        }

        return processedData;
    }

    /// <summary>
    /// 执行虚拟节点计算
    /// </summary>
    private async Task ExecuteVirtualNodesAsync(List<CollectedData> processedData, CancellationToken cancellationToken)
    {
        foreach (var data in processedData)
        {
            try
            {
                // 通过 Tag 触发依赖此数据点的虚拟节点计算
                var virtualResults = await _virtualNodeEngine.OnDependencyDataUpdatedAsync(
                    data.Tag, data.Value, cancellationToken);

                // 将虚拟节点计算结果也更新到快照
                await UpdateVirtualNodeResultsToSnapshotAsync(virtualResults, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "虚拟节点计算失败，数据点：{Tag}", data.Tag);
            }
        }
    }

    /// <summary>
    /// 将虚拟节点计算结果更新到快照和实时数据服务
    /// 使用虚拟数据点缓存，避免频繁数据库查询
    /// </summary>
    private async Task UpdateVirtualNodeResultsToSnapshotAsync(
        List<Domain.Interfaces.VirtualNodeCalculationResult> virtualResults,
        CancellationToken cancellationToken)
    {
        foreach (var virtualResult in virtualResults.Where(r => r.Success))
        {
            // 使用虚拟数据点的 Tag 查找虚拟数据点
            if (!string.IsNullOrEmpty(virtualResult.VirtualDataPointTag))
            {
                VirtualDataPoint? vp = null;

                // 先从缓存获取，缓存未命中时查询数据库
                if (!_virtualDataPointCache.TryGetValue(virtualResult.VirtualDataPointTag, out vp))
                {
                    vp = await FindVirtualDataPointByTagAsync(virtualResult.VirtualDataPointTag);
                    if (vp != null)
                    {
                        _virtualDataPointCache.TryAdd(virtualResult.VirtualDataPointTag, vp);
                        
                        // 更新设备虚拟数据点索引
                        _deviceVirtualPointIds.AddOrUpdate(
                            vp.DeviceId,
                            new HashSet<int> { -vp.Id },
                            (_, existing) =>
                            {
                                existing.Add(-vp.Id);
                                return existing;
                            });
                    }
                }

                if (vp != null)
                {
                    var snapshotKey = -vp.Id;

                    // 维护 Tag 索引
                    _tagIndex[vp.Tag] = snapshotKey;

                    // 更新快照（ConcurrentDictionary 直接赋值即可，无需锁）
                    _dataSnapshot[snapshotKey] = new TimestampedData
                    {
                        Data = new CollectedData
                        {
                            DataPointId = snapshotKey,
                            Tag = vp.Tag,
                            DeviceId = vp.DeviceId,
                            DeviceName = vp.Device?.Name ?? "Unknown",
                            Value = virtualResult.Value,
                            Quality = virtualResult.Quality,
                            Timestamp = virtualResult.Timestamp
                        },
                        LastUpdateTime = DateTime.UtcNow
                    };
                }
            }
        }
    }

    /// <summary>
    /// 清理指定设备的快照数据和 Tag 索引
    /// 批量操作，减少锁持有时间
    /// 使用 ConcurrentDictionary，无需锁
    /// </summary>
    private void ClearDeviceSnapshotData(int deviceId)
    {
        // 找出该设备的所有数据点
        var keysToRemove = _dataSnapshot
            .Where(kvp => kvp.Value.Data.DeviceId == deviceId)
            .Select(kvp => kvp.Key)
            .ToList();

        // 从快照中移除
        foreach (var key in keysToRemove)
        {
            _dataSnapshot.TryRemove(key, out _);
        }

        _logger.LogDebug("设备 ID={DeviceId} 的快照数据已清理，移除 {Count} 个数据点", deviceId, keysToRemove.Count);

        // 清理 Tag 索引（ConcurrentDictionary 本身线程安全）
        var tagKeysToRemove = _tagIndex
            .Where(kvp => !_dataSnapshot.ContainsKey(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var tagKey in tagKeysToRemove)
        {
            _tagIndex.TryRemove(tagKey, out _);
        }
    }

    /// <summary>
    /// 根据 Tag 查找虚拟数据点
    /// </summary>
    private async Task<VirtualDataPoint?> FindVirtualDataPointByTagAsync(string tag)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.VirtualDataPoints
            .Include(vp => vp.Device)
            .FirstOrDefaultAsync(vp => vp.Tag == tag);
    }
}
