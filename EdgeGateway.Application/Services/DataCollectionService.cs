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
/// 采集服务，负责调度所有设备的采集任务并维护实时快照。
/// </summary>
public class DataCollectionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CollectionStrategyRegistry _strategyRegistry;
    private readonly DataSendService _sendService;
    private readonly IRuleEngine _ruleEngine;
    private readonly IVirtualNodeEngine _virtualNodeEngine;
    private readonly DeviceRuntimeStateStore _runtimeStateStore;
    private readonly ILogger<DataCollectionService> _logger;
    private readonly GatewayOptions _options;

    private readonly ConcurrentDictionary<int, CancellationTokenSource> _deviceTasks = new();
    private readonly ConcurrentDictionary<int, TimestampedData> _dataSnapshot = new();
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
        DeviceRuntimeStateStore runtimeStateStore,
        ILogger<DataCollectionService> logger,
        IOptions<GatewayOptions> options)
    {
        _serviceProvider = serviceProvider;
        _strategyRegistry = strategyRegistry;
        _sendService = sendService;
        _ruleEngine = ruleEngine;
        _virtualNodeEngine = virtualNodeEngine;
        _runtimeStateStore = runtimeStateStore;
        _logger = logger;
        _options = options.Value;

        _aggregateWindowMs = _options.Collection.AggregateWindowMs;
        _dataExpiration = TimeSpan.FromSeconds(_options.Collection.DataExpirationSeconds);

        if (virtualNodeEngine is VirtualNodeEngine engine)
            engine.SetDataSnapshotGetter(GetSnapshotValue);
    }

    public RuntimeDeviceSnapshot GetDeviceRuntimeStatus(int deviceId)
    {
        return _runtimeStateStore.GetSnapshot(deviceId);
    }

    public Dictionary<int, RuntimeDeviceSnapshot> GetAllDeviceRuntimeStatuses()
    {
        return _runtimeStateStore.GetAllSnapshots();
    }

    private object? GetSnapshotValue(string tag)
    {
        // 虚拟节点按 Tag 读取快照时，先通过索引定位数据点，再校验是否过期。
        if (_tagIndex.TryGetValue(tag, out var dataPointId) &&
            _dataSnapshot.TryGetValue(dataPointId, out var snapshotData))
        {
            if (snapshotData.IsExpired(_dataExpiration))
                return null;

            return snapshotData.Data.Value;
        }

        return null;
    }

    public async Task StartAggregatorAsync(CancellationToken cancellationToken)
    {
        _aggregatorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _aggregatorTask = Task.Run(async () =>
        {
            try
            {
                // 聚合器按固定时间窗批量下发最新快照，避免每个采样点都立即触发发送。
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

        await LoadAndSetVirtualPointsAsync();
        StartVirtualNodeCalculator(cancellationToken);

        _logger.LogInformation("数据聚合器已启动，窗口间隔：{WindowMs}ms", _aggregateWindowMs);
    }

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
                engine.SetVirtualPoints(virtualPoints);

            _logger.LogInformation("虚拟数据点已加载，数量：{Count}", virtualPoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载虚拟数据点失败");
        }
    }

    private void StartVirtualNodeCalculator(CancellationToken cancellationToken)
    {
        _virtualNodeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _virtualNodeTask = Task.Run(async () =>
        {
            try
            {
                // 虚拟点独立于设备采集循环运行，持续基于最新快照做表达式计算。
                while (!_virtualNodeCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _virtualNodeCts.Token);

                    try
                    {
                        var results = _virtualNodeEngine.CalculateAll();

                        foreach (var result in results.Where(r => r.Success))
                        {
                            if (string.IsNullOrEmpty(result.VirtualDataPointTag))
                                continue;

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

    private async Task FlushSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_dataSnapshot.IsEmpty)
                return;

            // 发送阶段直接读取当前快照，确保一个窗口内每个点只保留最后一次有效值。
            var dataToSend = new List<CollectedData>(_dataSnapshot.Count);
            foreach (var kvp in _dataSnapshot)
                dataToSend.Add(kvp.Value.Data);

            await _sendService.DispatchAsync(dataToSend, cancellationToken);
            _logger.LogDebug("数据聚合推送：{Total} 个数据点", _dataSnapshot.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "数据聚合推送失败");
        }
    }

    public List<CollectedData> GetDeviceSnapshotData(int deviceId) =>
        _dataSnapshot.Values.Where(x => x.Data.DeviceId == deviceId).Select(x => x.Data).ToList();

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
            Quality = value != null ? DataQuality.Good : DataQuality.Bad,
            Timestamp = DateTime.UtcNow
        };

        return SetDataSnapshotAsync(collectedData);
    }

    public async Task StartAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

        var devices = (await deviceRepo.GetEnabledAsync()).ToList();
        _logger.LogInformation("共发现 {Count} 台启用设备，开始启动采集任务", devices.Count);

        await StartAggregatorAsync(cancellationToken);

        foreach (var device in devices)
            StartDeviceTask(device, cancellationToken);
    }

    private static int NormalizeRetryCount(Device device) => Math.Max(1, device.ReconnectRetryCount);

    private static int NormalizeRetryDelayMs(Device device) => Math.Max(100, device.ReconnectRetryDelayMs);

    private static int NormalizeReconnectIntervalMs(Device device) => Math.Max(500, device.ReconnectIntervalMs);

    private static int NormalizeReadFailureThreshold(Device device) => Math.Max(1, device.MaxConsecutiveReadFailures);

    private static int NormalizeReadFailureWindowSize(Device device) => Math.Max(3, device.ReadFailureWindowSize);

    private static double NormalizeReadFailureRateThreshold(Device device)
    {
        return Math.Clamp(device.ReadFailureRateThresholdPercent, 1, 100);
    }

    private async Task<bool> TryConnectWithRetryAsync(
        ICollectionStrategy strategy,
        Device device,
        RuntimeDeviceState state,
        int reconnectRound,
        CancellationToken cancellationToken)
    {
        var retryCount = NormalizeRetryCount(device);
        var retryDelayMs = NormalizeRetryDelayMs(device);

        state.CurrentReconnectRound = reconnectRound;
        state.CurrentReconnectAttempt = 0;

        for (var attempt = 1; attempt <= retryCount && !cancellationToken.IsCancellationRequested; attempt++)
        {
            state.CurrentReconnectAttempt = attempt;
            state.SetStatus(
                DeviceRuntimeStatus.Reconnecting,
                $"第 {reconnectRound} 轮重连，第 {attempt}/{retryCount} 次连接中");

            try
            {
                await strategy.ConnectAsync(device, cancellationToken);
                state.MarkConnected();

                _logger.LogInformation(
                    "设备 [{DeviceName}] 连接成功，第 {Attempt}/{RetryCount} 次尝试",
                    device.Name,
                    attempt,
                    retryCount);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                state.MarkConnectFailure(ex.Message);

                _logger.LogWarning(
                    ex,
                    "设备 [{DeviceName}] 连接失败，第 {Attempt}/{RetryCount} 次尝试",
                    device.Name,
                    attempt,
                    retryCount);

                if (attempt < retryCount)
                    await Task.Delay(retryDelayMs, cancellationToken);
            }
        }

        return false;
    }

    private void StartDeviceTask(Device device, CancellationToken parentToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        _deviceTasks[device.Id] = cts;

        var state = _runtimeStateStore.GetOrAddState(device.Id, device.Name);

        _ = Task.Run(async () =>
        {
            _logger.LogInformation(
                "设备 [{DeviceName}] 采集任务启动，协议：{Protocol}，周期：{Interval}ms",
                device.Name,
                device.Protocol,
                device.PollingIntervalMs);

            var strategy = _strategyRegistry.Resolve(device.Protocol);
            var enabledPoints = device.DataPoints.Where(dp => dp.IsEnabled).ToList();

            if (!enabledPoints.Any())
            {
                state.SetStatus(DeviceRuntimeStatus.Warning, "未配置启用的数据点");
                _logger.LogWarning("设备 [{DeviceName}] 没有启用的数据点，跳过采集", device.Name);
                return;
            }

            var reconnectIntervalMs = NormalizeReconnectIntervalMs(device);
            var readFailureThreshold = NormalizeReadFailureThreshold(device);
            var readFailureWindowSize = NormalizeReadFailureWindowSize(device);
            var readFailureRateThreshold = NormalizeReadFailureRateThreshold(device);
            var reconnectRound = 0;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    reconnectRound++;
                    // 每一轮先完成连接，成功后再进入稳定采集循环；失败则按设备策略等待重连。
                    var connected = await TryConnectWithRetryAsync(strategy, device, state, reconnectRound, cts.Token);
                    if (!connected)
                    {
                        if (!device.ReconnectEnabled)
                        {
                            state.SetStatus(DeviceRuntimeStatus.Error, "连接失败，且已禁用自动重连");
                            _logger.LogWarning("设备 [{DeviceName}] 连接失败且未启用重连，采集任务结束", device.Name);
                            break;
                        }

                        state.SetStatus(
                            DeviceRuntimeStatus.Reconnecting,
                            $"本轮连接失败，{reconnectIntervalMs}ms 后继续下一轮重连");

                        _logger.LogWarning(
                            "设备 [{DeviceName}] 当前重连轮次失败，{ReconnectIntervalMs}ms 后继续重连",
                            device.Name,
                            reconnectIntervalMs);

                        await Task.Delay(reconnectIntervalMs, cts.Token);
                        continue;
                    }

                    var consecutiveReadFailures = 0;
                    state.ResetReadFailureWindow();

                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var cycleStart = DateTime.UtcNow;

                            try
                            {
                                // 采集策略内部负责协议读写，本层只接收结果并写入统一快照。
                                await strategy.ReadAsync(enabledPoints, SetDataSnapshot, cts.Token);
                                consecutiveReadFailures = 0;
                                state.MarkReadSuccess();
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                consecutiveReadFailures++;
                                state.MarkReadFailure(ex.Message, consecutiveReadFailures);

                                _logger.LogError(
                                    ex,
                                    "设备 [{DeviceName}] 读取失败，第 {FailureCount}/{FailureThreshold} 次连续失败",
                                    device.Name,
                                    consecutiveReadFailures,
                                    readFailureThreshold);

                                if (consecutiveReadFailures >= readFailureThreshold)
                                {
                                    state.SetStatus(
                                        DeviceRuntimeStatus.Reconnecting,
                                        $"连续读取失败达到阈值 {readFailureThreshold}，准备重连");

                                    _logger.LogWarning("设备 [{DeviceName}] 连续读取失败达到阈值，准备重连", device.Name);
                                    break;
                                }

                                if (state.ShouldReconnectByFailureRate(readFailureWindowSize, readFailureRateThreshold))
                                {
                                    state.SetStatus(
                                        DeviceRuntimeStatus.Reconnecting,
                                        $"读取失败比例达到 {state.ReadFailureRatePercent:F0}% ，准备重连");

                                    _logger.LogWarning(
                                        "设备 [{DeviceName}] 读取失败比例达到阈值，当前失败率：{FailureRate:F2}%",
                                        device.Name,
                                        state.ReadFailureRatePercent);
                                    break;
                                }
                            }

                            var elapsed = (DateTime.UtcNow - cycleStart).TotalMilliseconds;
                            var waitMs = Math.Max(0, device.PollingIntervalMs - elapsed);
                            await Task.Delay((int)waitMs, cts.Token);
                        }
                    }
                    finally
                    {
                        state.MarkDisconnected();
                        await strategy.DisconnectAsync();
                    }

                    if (!device.ReconnectEnabled || cts.Token.IsCancellationRequested)
                        break;

                    state.SetStatus(
                        DeviceRuntimeStatus.Reconnecting,
                        $"等待 {reconnectIntervalMs}ms 后开始下一轮重连");

                    _logger.LogInformation(
                        "设备 [{DeviceName}] 将在 {ReconnectIntervalMs}ms 后发起下一轮重连",
                        device.Name,
                        reconnectIntervalMs);
                    await Task.Delay(reconnectIntervalMs, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                state.SetStatus(DeviceRuntimeStatus.Stopped, "采集任务已停止");
                _logger.LogInformation("设备 [{DeviceName}] 采集任务已取消", device.Name);
            }
            finally
            {
                state.MarkDisconnected();
                await strategy.DisconnectAsync();
                _logger.LogInformation("设备 [{DeviceName}] 连接已断开", device.Name);
            }
        }, cts.Token);
    }

    public void StopDevice(int deviceId)
    {
        if (_deviceTasks.TryRemove(deviceId, out var cts))
        {
            cts.Cancel();
            ClearDeviceSnapshotData(deviceId);
        }

        _runtimeStateStore.MarkStopped(deviceId, "Device stopped");
        _logger.LogInformation("设备 ID [{DeviceId}] 采集任务已停止", deviceId);
    }

    public void StopAll()
    {
        foreach (var cts in _deviceTasks.Values)
            cts.Cancel();

        _deviceTasks.Clear();

        _runtimeStateStore.MarkAllStopped("All collection tasks stopped");
        _logger.LogInformation("所有采集任务已停止");
    }

    public async Task ReloadDeviceAsync(int deviceId, CancellationToken cancellationToken)
    {
        StopDevice(deviceId);

        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepo.GetByIdAsync(deviceId);

        if (device == null || !device.IsEnabled)
        {
            _runtimeStateStore.MarkStopped(deviceId, "Device is disabled or missing");
            _logger.LogInformation("设备 ID={DeviceId} 未启用或不存在，跳过重新加载", deviceId);
            return;
        }

        StartDeviceTask(device, cancellationToken);
        _logger.LogInformation("设备 ID={DeviceId} 配置已重新加载", deviceId);
    }

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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await FlushSnapshotAsync(cts.Token);
        _logger.LogInformation("快照已刷新");
    }

    public async Task StartDeviceTaskByIdAsync(int deviceId)
    {
        if (_deviceTasks.TryRemove(deviceId, out var existingCts))
        {
            await existingCts.CancelAsync();
            ClearDeviceSnapshotData(deviceId);
            _logger.LogInformation("设备 ID [{DeviceId}] 原有采集任务已停止，准备重启", deviceId);
        }

        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepo.GetByIdAsync(deviceId);

        if (device == null || !device.IsEnabled)
        {
            _runtimeStateStore.MarkStopped(deviceId, "Device is missing or disabled");
            _logger.LogWarning("设备 ID [{DeviceId}] 不存在或未启用，无法启动采集", deviceId);
            return;
        }

        StartDeviceTask(device, CancellationToken.None);
    }

    private void ClearDeviceSnapshotData(int deviceId)
    {
        var keysToRemove = _dataSnapshot
            .Where(kvp => kvp.Value.Data.DeviceId == deviceId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
            _dataSnapshot.TryRemove(key, out _);

        _logger.LogDebug("设备 ID={DeviceId} 的快照数据已清理，移除 {Count} 个数据点", deviceId, keysToRemove.Count);

        var tagKeysToRemove = _tagIndex
            .Where(kvp => !_dataSnapshot.ContainsKey(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var tagKey in tagKeysToRemove)
            _tagIndex.TryRemove(tagKey, out _);
    }

    private async Task SetDataSnapshotAsync(CollectedData collectedData)
    {
        if (collectedData.DataPointId >= 0)
        {
            try
            {
                // 实体数据点先经过规则引擎，虚拟点则跳过，避免重复处理表达式输出。
                var ruleResult = await _ruleEngine.ExecuteRulesAsync(collectedData, CancellationToken.None);

                if (ruleResult.ShouldReject)
                {
                    _logger.LogDebug("数据点 {Tag} 被规则拒绝：{Error}", collectedData.Tag, ruleResult.ErrorMessage);
                    return;
                }

                if (ruleResult.Value != null)
                    collectedData.Value = ruleResult.Value;

                collectedData.Quality = ruleResult.Quality;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "规则执行失败，数据点：{Tag}", collectedData.Tag);
            }
        }

        _tagIndex[collectedData.Tag] = collectedData.DataPointId;

        // 快照只保留每个数据点的最新有效值，并用时间戳控制过期清理。
        if (_dataSnapshot.TryGetValue(collectedData.DataPointId, out var existing))
        {
            if (existing.IsExpired(_dataExpiration))
            {
                existing.Data = collectedData;
                existing.LastUpdateTime = collectedData.Value != null ? DateTime.UtcNow : DateTime.MinValue;
            }
            else if (collectedData.Value != null)
            {
                existing.Data = collectedData;
                existing.LastUpdateTime = DateTime.UtcNow;
            }
        }
        else
        {
            _dataSnapshot[collectedData.DataPointId] = new TimestampedData
            {
                Data = collectedData,
                LastUpdateTime = collectedData.Value != null ? DateTime.UtcNow : DateTime.MinValue
            };
        }
    }

    private void SetDataSnapshot(CollectedData collectedData)
    {
        SetDataSnapshotAsync(collectedData).ConfigureAwait(false).GetAwaiter().GetResult();
    }

}
