using System.Collections.Concurrent;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Domain.Options;
using EdgeGateway.Infrastructure.Data;
using EdgeGateway.Infrastructure.VirtualNodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly ILogger<DataCollectionService> _logger;
    private readonly GatewayOptions _options;

    // 每个设备的采集任务句柄（设备 Id → CancellationTokenSource）
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _deviceTasks = new();

    // 全量数据快照：DataPointId → 带时间戳的采集数据（唯一数据源）
    // 使用 ConcurrentDictionary 替代 ReaderWriterLockSlim + Dictionary，提升并发性能
    private readonly ConcurrentDictionary<int, TimestampedData> _dataSnapshot = new();

    // Tag 索引：Tag → DataPointId，支持按 Tag 快速查询
    private readonly ConcurrentDictionary<string, int> _tagIndex = new();
    


    private readonly int _aggregateWindowMs;
    private readonly TimeSpan _dataExpiration;
    private CancellationTokenSource? _aggregatorCts;
    private Task? _aggregatorTask;
    private CancellationTokenSource? _virtualNodeCts;
    private Task? _virtualNodeTask;

    public DataCollectionService(
        IServiceProvider serviceProvider,
        CollectionStrategyRegistry strategyRegistry,
        DataSendService sendService,
        IRuleEngine ruleEngine,
        IVirtualNodeEngine virtualNodeEngine,
        ILogger<DataCollectionService> logger,
        IOptions<GatewayOptions> options)
    {
        _serviceProvider = serviceProvider;
        _strategyRegistry = strategyRegistry;
        _sendService = sendService;
        _ruleEngine = ruleEngine;
        _virtualNodeEngine = virtualNodeEngine;
        _logger = logger;
        _options = options.Value;

        // 从配置读取采集相关参数
        _aggregateWindowMs = _options.Collection.AggregateWindowMs;
        _dataExpiration = TimeSpan.FromSeconds(_options.Collection.DataExpirationSeconds);

        // 设置虚拟节点引擎的快照数据获取委托
        if (virtualNodeEngine is VirtualNodeEngine engine)
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

        // 加载虚拟数据点并设置到引擎
        await LoadAndSetVirtualPointsAsync();

        // 启动虚拟节点定时计算任务（每秒执行一次）
        StartVirtualNodeCalculator(cancellationToken);

        _logger.LogInformation("数据聚合器已启动，窗口间隔：{WindowMs}ms", _aggregateWindowMs);
    }

    /// <summary>
    /// 从数据库加载虚拟数据点并设置到引擎
    /// </summary>
    private async Task LoadAndSetVirtualPointsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

            var virtualPoints = await context.VirtualDataPoints
                .Include(p => p.Device)
                .Where(p => p.IsEnabled)
                .ToListAsync();

            if (_virtualNodeEngine is VirtualNodeEngine engine)
            {
                engine.SetVirtualPoints(virtualPoints);
            }

            _logger.LogInformation("虚拟数据点已加载：{Count} 个", virtualPoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载虚拟数据点失败");
        }
    }

    /// <summary>
    /// 启动虚拟节点定时计算任务（每秒从快照获取数据进行计算）
    /// </summary>
    private void StartVirtualNodeCalculator(CancellationToken cancellationToken)
    {
        _virtualNodeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _virtualNodeTask = Task.Run(async () =>
        {
            try
            {
                while (!_virtualNodeCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _virtualNodeCts.Token);

                    try
                    {
                        // 计算所有虚拟节点（从快照获取依赖数据）
                        var results = _virtualNodeEngine.CalculateAll();

                        // 将计算结果通过 SetDataSnapshot 更新回快照
                        foreach (var result in results.Where(r => r.Success))
                        {
                            if (!string.IsNullOrEmpty(result.VirtualDataPointTag))
                            {
                                // 从虚拟节点引擎获取设备 ID
                                var deviceId = _virtualNodeEngine.GetDeviceId(result.VirtualDataPointId);

                                var virtualData = new CollectedData
                                {
                                    DataPointId = -result.VirtualDataPointId,
                                    Tag = result.VirtualDataPointTag,
                                    DeviceId = deviceId,
                                    DeviceName = "VirtualNode",
                                    Value = result.Value,
                                    Quality = result.Quality,
                                    Timestamp = DateTime.UtcNow
                                };
                                await SetDataSnapshotAsync(virtualData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "虚拟节点定时计算失败");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("虚拟节点计算器已停止");
            }
        }, _virtualNodeCts.Token);

        _logger.LogInformation("虚拟节点定时计算器已启动，间隔：1000ms");
    }
    


    /// <summary>
    /// 将全量快照数据批量推送给发送服务（超过 30 秒未更新的数据置为 null）
    /// 使用 ConcurrentDictionary，无需锁，支持读取期间并发写入
    /// 启用变化检测时，只发送值发生变化的数据点
    /// </summary>
    private async Task FlushSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_dataSnapshot.IsEmpty)
                return;

            // 预分配列表容量，避免扩容开销
            var dataToSend = new List<CollectedData>(_dataSnapshot.Count);
            var changedCount = 0;

            foreach (var kvp in _dataSnapshot)
            {
                dataToSend.Add(kvp.Value.Data);
            }
            
            await _sendService.DispatchAsync(dataToSend, cancellationToken);
            _logger.LogDebug("数据聚合推送：{Changed}/{Total} 个数据点有变化", changedCount, _dataSnapshot.Count);
            // }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "数据聚合推送失败");
        }
    }




    /// <summary>
    /// 获取指定设备的所有数据点快照数据
    /// </summary>
    public List<CollectedData> GetDeviceSnapshotData(int deviceId) =>
        _dataSnapshot.Values.Where(x => x.Data.DeviceId == deviceId).Select(x => x.Data).ToList();

    /// <summary>
    /// 外部写入点位成功后，直接更新快照，保证前端能立即看到结果。
    /// </summary>
    public Task OverrideDataPointValueAsync(DataPoint dataPoint, object? value, string deviceCode)
    {
        var collectedData = new CollectedData
        {
            DataPointId = dataPoint.Id,
            Tag = dataPoint.Tag,
            DeviceId = dataPoint.DeviceId,
            DeviceName = deviceCode,
            Value = value,
            Unit = dataPoint.Unit,
            Quality = value != null ? EdgeGateway.Domain.Interfaces.DataQuality.Good : EdgeGateway.Domain.Interfaces.DataQuality.Bad,
            Timestamp = DateTime.UtcNow
        };

        return SetDataSnapshotAsync(collectedData);
    }

    /// <summary>
    /// 在设备级通信锁内执行操作，避免采集与控制并发访问同一设备。
    /// </summary>


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
        // await InitializeVirtualNodesAsync(cancellationToken);

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
        _ = Task.Run((Func<Task>)(async () =>
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
                        // 采集策略会通过 SetDataSnapshot 回调实时更新快照（带过期判断和规则引擎）
                        await strategy.ReadAsync(enabledPoints, SetDataSnapshot, cts.Token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // 采集异常不中断循环，记录日志后等待下一周期
                        _logger.LogError(ex, "设备 [{DeviceName}] 单次采集失败，等待下一周期重试", device.Name);
                    }

                    // 计算剩余等待时间（扣除采集耗时，尽量保证周期准确）
                    var elapsed = (DateTime.UtcNow - cycleStart).TotalMilliseconds;
                    var waitMs = Math.Max(0, device.PollingIntervalMs - elapsed);
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
        }), cts.Token);
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
                catch (OperationCanceledException)
                {
                }
            }

            _logger.LogInformation("数据聚合器已停止");
        }

        // 停止虚拟节点计算器
        if (_virtualNodeCts != null)
        {
            await _virtualNodeCts.CancelAsync();
            _virtualNodeCts.Dispose();

            if (_virtualNodeTask != null)
            {
                try
                {
                    await _virtualNodeTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            _logger.LogInformation("虚拟节点计算器已停止");
        }

        // 刷新剩余快照数据
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await FlushSnapshotAsync(cts.Token);

        _logger.LogInformation("快照已刷新");
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
        // await InitializeDeviceVirtualNodesAsync(deviceId, CancellationToken.None);
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
    /// 设置数据快照，带过期时间判断和规则引擎处理
    /// 如果数据已过期（超过配置的时间未更新），则设置为 null
    /// 如果数据未过期，则使用上一次的数据
    /// 采集到每个数据点时立即触发规则引擎
    /// </summary>
    /// <param name="collectedData">要设置的数据</param>
    private async Task SetDataSnapshotAsync(CollectedData collectedData)
    {
        // 执行规则引擎（虚拟节点数据跳过规则引擎）
        if (collectedData.DataPointId >= 0)
        {
            try
            {
                var ruleResult = await _ruleEngine.ExecuteRulesAsync(collectedData, CancellationToken.None);

                if (ruleResult.ShouldReject)
                {
                    _logger.LogDebug("数据点 {Tag} 被规则拒绝：{Error}", collectedData.Tag, ruleResult.ErrorMessage);
                    return;
                }

                // 使用规则处理后的值
                if (ruleResult.Value != null)
                {
                    collectedData.Value = ruleResult.Value;
                }

                collectedData.Quality = ruleResult.Quality;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "规则执行失败，数据点：{Tag}", collectedData.Tag);
                // 规则执行失败时保留原数据
            }
        }

        // 维护 Tag 索引
        _tagIndex[collectedData.Tag] = collectedData.DataPointId;

        // 更新或添加数据点的最新值
        if (_dataSnapshot.TryGetValue(collectedData.DataPointId, out var existing))
        {
            // 检查现有数据是否已过期
            if (existing.IsExpired(_dataExpiration))
            {
                // 已过期，设置为新数据（如果 Value 不为 null）
                if (collectedData.Value != null)
                {
                    existing.Data = collectedData;
                    existing.LastUpdateTime = DateTime.UtcNow;
                }
                else
                {
                    // Value 为 null 且已过期，保持过期状态
                    existing.Data = collectedData;
                    existing.LastUpdateTime = DateTime.MinValue;
                }
            }
            else
            {
                // 未过期，只有 Value 不为 null 时才更新数据和 LastUpdateTime
                if (collectedData.Value != null)
                {
                    existing.Data = collectedData;
                    existing.LastUpdateTime = DateTime.UtcNow;
                }
                // 如果 Value 为 null，保持 existing.Data 和 LastUpdateTime 不变
                // 这样超过 30 秒后会自动过期
            }
        }
        else
        {
            // 新数据点，直接添加
            _dataSnapshot[collectedData.DataPointId] = new TimestampedData
            {
                Data = collectedData,
                LastUpdateTime = collectedData.Value != null ? DateTime.UtcNow : DateTime.MinValue
            };
        }
    }

    /// <summary>
    /// 设置数据快照（同步包装器，用于兼容 Action&lt;CollectedData&gt; 回调）
    /// </summary>
    private void SetDataSnapshot(CollectedData collectedData)
    {
        // 由于此方法在后台线程中被调用，使用 ConfigureAwait(false) 避免死锁
        SetDataSnapshotAsync(collectedData).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
