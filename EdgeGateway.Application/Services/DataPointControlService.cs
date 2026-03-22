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
        // 校验点位是否存在，且属于目标设备
        var dataPoint = await _dataPointRepository.GetByIdAsync(dataPointId)
            ?? throw new InvalidOperationException($"Data point ID={dataPointId} was not found");

        if (dataPoint.DeviceId != deviceId)
            throw new InvalidOperationException($"Data point ID={dataPointId} does not belong to device ID={deviceId}");

        if (!dataPoint.IsEnabled)
            throw new InvalidOperationException("The target data point is disabled");

        var device = await _deviceRepository.GetByIdAsync(deviceId)
            ?? throw new InvalidOperationException($"Device ID={deviceId} was not found");

        if (!device.IsEnabled)
            throw new InvalidOperationException("The target device is disabled");

        // 根据设备协议解析具体的读写策略
        var strategy = _strategyRegistry.Resolve(device.Protocol);
        object? actualValue = null;

        await _collectionService.ExecuteWithDeviceLockAsync(
            deviceId,
            async token =>
            {
                try
                {
                    await strategy.ConnectAsync(device, token);
                    await strategy.WriteAsync(dataPoint, value, token);

                    CollectedData? readBack = null;
                    await strategy.ReadAsync(
                        [dataPoint],
                        collected => readBack = collected,
                        token);

                    actualValue = readBack?.Value ?? value;

                    // 写入成功后立即覆盖内存快照，确保前端实时值同步刷新
                    await _collectionService.OverrideDataPointValueAsync(dataPoint, actualValue, device.Code);
                }
                finally
                {
                    await strategy.DisconnectAsync(token);
                }
            },
            cancellationToken);

        _logger.LogInformation(
            "Point control succeeded: Device={DeviceCode}, Tag={Tag}, Value={Value}",
            device.Code,
            dataPoint.Tag,
            actualValue);

        return actualValue;
    }
}
