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
/// 4. 将采集结果通过事件/回调传递给发送服务
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
    /// 启动所有启用设备的采集任务
    /// 每个设备独立运行一个后台采集循环（互不阻塞）
    /// </summary>
    public async Task StartAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        
        var devices = (await deviceRepo.GetEnabledAsync()).ToList();
        _logger.LogInformation("共发现 {Count} 台启用设备，开始启动采集任务", devices.Count);

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

                        // 将采集数据推送给发送服务（异步，不阻塞当前采集周期）
                        _ = _sendService.DispatchAsync(collectedData, cts.Token);
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
