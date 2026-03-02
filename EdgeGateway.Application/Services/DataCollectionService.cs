using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
/// </summary>
public class DataCollectionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CollectionStrategyRegistry _strategyRegistry;
    private readonly DataSendService _sendService;
    private readonly RealtimeDataService _realtimeDataService;
    private readonly ILogger<DataCollectionService> _logger;

    // 每个设备的采集任务句柄（设备 Id → CancellationTokenSource）
    private readonly Dictionary<int, CancellationTokenSource> _deviceTasks = new();

    // 全量数据快照：DataPointId → 带时间戳的采集数据
    private readonly Dictionary<int, TimestampedData> _dataSnapshot = new();
    private readonly SemaphoreSlim _snapshotLock = new(1, 1);
    private readonly int _aggregateWindowMs = 1000; // 聚合窗口：1 秒
    private readonly int _dataTimeoutMinutes = 1;   // 数据超时时间：1 分钟
    private CancellationTokenSource? _aggregatorCts;
    private Task? _aggregatorTask;

    /// <summary>
    /// 带时间戳的数据包装
    /// </summary>
    private class TimestampedData
    {
        public CollectedData Data { get; set; } = null!;
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
    }

    public DataCollectionService(
        IServiceProvider serviceProvider,
        CollectionStrategyRegistry strategyRegistry,
        DataSendService sendService,
        RealtimeDataService realtimeDataService,
        ILogger<DataCollectionService> logger)
    {
        _serviceProvider     = serviceProvider;
        _strategyRegistry    = strategyRegistry;
        _sendService         = sendService;
        _realtimeDataService = realtimeDataService;
        _logger              = logger;
    }

    /// <summary>
    /// 启动数据聚合器（后台任务，按窗口间隔批量推送全量数据）
    /// </summary>
    public void StartAggregator(CancellationToken cancellationToken)
    {
        // 预先加载所有通道绑定的数据点，确保即使未采集过也会出现在快照中
        InitializeSnapshotFromMappings();

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
    /// </summary>
    private void InitializeSnapshotFromMappings()
    {
        using var scope = _serviceProvider.CreateScope();
        var channelRepo = scope.ServiceProvider.GetRequiredService<IChannelRepository>();

        var channels = channelRepo.GetEnabledWithMappingsAsync().Result;
        foreach (var channel in channels)
        {
            foreach (var mapping in channel.DataPointMappings.Where(m => m.IsEnabled))
            {
                if (!_dataSnapshot.ContainsKey(mapping.DataPointId))
                {
                    // 创建初始占位数据
                    _dataSnapshot[mapping.DataPointId] = new TimestampedData
                    {
                        Data = new CollectedData
                        {
                            DataPointId = mapping.DataPointId,
                            Tag = mapping.DataPoint?.Tag ?? "Unknown",
                            DeviceId = mapping.DataPoint?.DeviceId ?? 0,
                            DeviceName = mapping.DataPoint?.Device?.Name ?? "Unknown",
                            Value = null,
                            Quality = DataQuality.Uncertain,
                            Timestamp = DateTime.UtcNow
                        },
                        LastUpdateTime = DateTime.MinValue // 使用 MinValue，这样会被视为超时
                    };
                }
            }
        }

        _logger.LogInformation("快照初始化完成，共 {Count} 个数据点", _dataSnapshot.Count);
    }

    /// <summary>
    /// 将全量快照数据批量推送给发送服务（超过 1 分钟未更新的数据置为 null）
    /// </summary>
    private async Task FlushSnapshotAsync(CancellationToken cancellationToken)
    {
        await _snapshotLock.WaitAsync(cancellationToken);
        try
        {
            if (_dataSnapshot.Count == 0)
                return;

            var now = DateTime.UtcNow;
            var timeoutThreshold = now.AddMinutes(-_dataTimeoutMinutes);

            // 复制当前全量快照，超时数据置为 null
            var dataToSend = new List<CollectedData>();
            foreach (var kvp in _dataSnapshot)
            {
                // LastUpdateTime 为 MinValue 表示初始占位数据，保持 Uncertain 状态
                // 超过 1 分钟未更新的数据也置为 Uncertain
                if (kvp.Value.LastUpdateTime == DateTime.MinValue)
                {
                    // 初始占位数据，保持原样（Uncertain）
                    dataToSend.Add(kvp.Value.Data);
                }
                else if (kvp.Value.LastUpdateTime < timeoutThreshold)
                {
                    // 超过 1 分钟未更新，创建 null 占位数据
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

            _logger.LogDebug("数据聚合推送：{Count} 条数据", dataToSend.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "数据聚合推送失败");
        }
        finally
        {
            _snapshotLock.Release();
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
    /// 只更新质量为 Good 的数据，Bad/Uncertain 数据保持上次成功值
    /// </summary>
    public async Task UpdateSnapshotAsync(IEnumerable<CollectedData> data, CancellationToken cancellationToken)
    {
        await _snapshotLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var item in data)
            {
                // 只更新质量为 Good 的数据
                if (item.Quality != DataQuality.Good)
                {
                    // 采集失败时，保持快照中的上次成功值，不更新
                    continue;
                }

                // 更新或添加数据点的最新值
                if (_dataSnapshot.TryGetValue(item.DataPointId, out var existing))
                {
                    existing.Data = item;
                    existing.LastUpdateTime = DateTime.UtcNow;
                }
                else
                {
                    _dataSnapshot[item.DataPointId] = new TimestampedData
                    {
                        Data = item,
                        LastUpdateTime = DateTime.UtcNow
                    };
                }
            }
        }
        finally
        {
            _snapshotLock.Release();
        }
    }

    /// <summary>
    /// 获取指定设备的所有数据点快照数据（用于 realtime 接口）
    /// 超过 1 分钟未更新的数据返回 null（Uncertain）
    /// </summary>
    public List<CollectedData> GetDeviceSnapshotData(int deviceId)
    {
        var now = DateTime.UtcNow;
        var timeoutThreshold = now.AddMinutes(-_dataTimeoutMinutes);

        var results = new List<CollectedData>();
        foreach (var kvp in _dataSnapshot)
        {
            if (kvp.Value.Data.DeviceId != deviceId)
                continue;

            // LastUpdateTime 为 MinValue 表示初始占位数据，保持 Uncertain 状态
            if (kvp.Value.LastUpdateTime == DateTime.MinValue)
            {
                results.Add(kvp.Value.Data);
            }
            else if (kvp.Value.LastUpdateTime < timeoutThreshold)
            {
                // 超过 1 分钟未更新，返回 null 占位数据
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
        StartAggregator(cancellationToken);

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

                        var goodCount = collectedData.Count(d => d.Quality == DataQuality.Good);
                        _logger.LogDebug("设备 [{DeviceName}] 采集完成：{Good}/{Total} 点质量良好",
                            device.Name, goodCount, collectedData.Count);

                        // 更新实时数据服务
                        _realtimeDataService.UpdateData(collectedData);

                        // 更新到全量快照（由聚合器按窗口间隔批量推送）
                        await UpdateSnapshotAsync(collectedData, cts.Token);
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
    /// 停止指定设备的采集任务
    /// </summary>
    public void StopDevice(int deviceId)
    {
        if (_deviceTasks.TryGetValue(deviceId, out var cts))
        {
            cts.Cancel();
            _deviceTasks.Remove(deviceId);
            _realtimeDataService.ClearDeviceData(deviceId);
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
        if (_deviceTasks.TryGetValue(deviceId, out var existingCts))
        {
            await existingCts.CancelAsync();
            _deviceTasks.Remove(deviceId);
            _realtimeDataService.ClearDeviceData(deviceId);
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
    }
}
