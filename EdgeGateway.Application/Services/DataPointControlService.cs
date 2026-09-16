using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 点位控制服务。
/// 负责校验设备与点位状态，调用对应协议策略执行写入，并刷新实时快照。
/// </summary>
public class DataPointControlService
{
    private readonly IDataPointRepository _dataPointRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly CollectionStrategyRegistry _strategyRegistry;
    private readonly DataCollectionService _collectionService;
    private readonly ILogger<DataPointControlService> _logger;

    public DataPointControlService(
        IDataPointRepository dataPointRepository,
        IDeviceRepository deviceRepository,
        CollectionStrategyRegistry strategyRegistry,
        DataCollectionService collectionService,
        ILogger<DataPointControlService> logger)
    {
        _dataPointRepository = dataPointRepository;
        _deviceRepository = deviceRepository;
        _strategyRegistry = strategyRegistry;
        _collectionService = collectionService;
        _logger = logger;
    }

    public async Task<object?> ControlAsync(int deviceId, int dataPointId, object? value, CancellationToken cancellationToken = default)
    {
        var dataPoint = await _dataPointRepository.GetByIdAsync(dataPointId)
            ?? throw new InvalidOperationException($"Data point ID={dataPointId} was not found");

        if (dataPoint.DeviceId != deviceId)
            throw new InvalidOperationException($"Data point ID={dataPointId} does not belong to device ID={deviceId}");

        if (!dataPoint.IsEnabled)
            throw new InvalidOperationException("The target data point is disabled");

        if (!dataPoint.IsControllable)
            throw new InvalidOperationException("The target data point is not controllable");

        var device = await _deviceRepository.GetByIdAsync(deviceId)
            ?? throw new InvalidOperationException($"Device ID={deviceId} was not found");

        if (!device.IsEnabled)
            throw new InvalidOperationException("The target device is disabled");

        var strategy = _strategyRegistry.Resolve(device.Protocol, device.Id);
        object? actualValue = null;

        try
        {
            // 采集任务运行时复用其策略实例和 TCP 连接；ConnectAsync 对已连接设备是幂等的。
            await strategy.ConnectAsync(device, cancellationToken);
            await strategy.WriteAsync(dataPoint, value, cancellationToken);

            CollectedData? readBack = null;
            try
            {
                await strategy.ReadAsync([dataPoint], collected => readBack = collected, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _collectionService.OverrideDataPointValueAsync(dataPoint, null, device.Code, DataQuality.Bad);
                throw new InvalidOperationException("Write completed, but read-back failed; the device value is unconfirmed.", ex);
            }

            actualValue = readBack?.Value;
            var quality = actualValue == null ? DataQuality.Bad : readBack!.Quality;
            await _collectionService.OverrideDataPointValueAsync(dataPoint, actualValue, device.Code, quality);
            if (quality != DataQuality.Good)
                throw new InvalidOperationException("Write completed, but read-back quality is not Good; the device value is unconfirmed.");
        }
        finally
        {
            // 采集任务拥有连接生命周期。没有采集任务时，控制请求负责释放临时连接。
            if (!_collectionService.IsDeviceCollecting(device.Id))
            {
                try { await strategy.DisconnectAsync(CancellationToken.None); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to disconnect after point control: {Tag}", dataPoint.Tag); }
            }
        }
        _logger.LogInformation(
            "Point control succeeded: Device={DeviceCode}, Tag={Tag}, Value={Value}",
            device.Code,
            dataPoint.Tag,
            actualValue);

        return actualValue;
    }

    public async Task<(DataPoint DataPoint, object? Value)> ControlByTagAsync(string tag, object? value, CancellationToken cancellationToken = default)
    {
        var dataPoint = await _dataPointRepository.GetByTagAsync(tag)
            ?? throw new InvalidOperationException($"Data point Tag='{tag}' was not found");

        var actualValue = await ControlAsync(dataPoint.DeviceId, dataPoint.Id, value, cancellationToken);
        return (dataPoint, actualValue);
    }
}
