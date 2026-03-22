using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

/// <summary>
/// 模拟采集策略。
/// 用于本地联调和演示环境，读操作返回随机值，写操作直接回显目标值。
/// </summary>
public class SimulatorCollectionStrategy : ICollectionStrategy
{
    private readonly ILogger<SimulatorCollectionStrategy> _logger;
    private readonly Random _random = new();
    private Device? _currentDevice;

    public SimulatorCollectionStrategy(ILogger<SimulatorCollectionStrategy> logger)
    {
        _logger = logger;
    }

    public string ProtocolName => "Simulator";

    public bool IsConnected => _currentDevice != null;

    public Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        _currentDevice = device;
        _logger.LogInformation("Simulator connected to device: {DeviceName}", device.Name);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulator disconnected from device: {DeviceName}", _currentDevice?.Name);
        _currentDevice = null;
        return Task.CompletedTask;
    }

    public Task ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        Action<CollectedData> callback,
        CancellationToken cancellationToken = default)
    {
        if (_currentDevice == null)
            throw new InvalidOperationException("Device is not connected. Call ConnectAsync first.");

        foreach (var dp in dataPoints)
        {
            // 模拟 10% 的坏质量数据，便于前端和规则链路联调
            var quality = _random.NextDouble() < 0.1 ? DataQuality.Bad : DataQuality.Good;

            object? value = quality == DataQuality.Bad ? null : dp.DataType switch
            {
                DataValueType.Bool => _random.Next(0, 2) == 1,
                DataValueType.Int16 => (short)_random.Next(-1000, 1000),
                DataValueType.Int32 => _random.Next(-100000, 100000),
                DataValueType.Float => Math.Round(_random.NextDouble() * 100, 2),
                DataValueType.Double => Math.Round(_random.NextDouble() * 1000, 4),
                DataValueType.String => $"simulated_{_random.Next(1000)}",
                _ => null
            };

            callback(new CollectedData
            {
                Tag = dp.Tag,
                DataPointId = dp.Id,
                DeviceId = _currentDevice.Id,
                DeviceName = _currentDevice.Code,
                Value = value,
                Unit = dp.Unit,
                Quality = quality,
                Timestamp = DateTime.UtcNow
            });
        }

        _logger.LogDebug("Simulator collected {Count} data points", dataPoints.Count());
        return Task.CompletedTask;
    }

    public Task<object?> WriteAsync(DataPoint dataPoint, object? value, CancellationToken cancellationToken = default)
    {
        if (_currentDevice == null)
            throw new InvalidOperationException("Device is not connected. Call ConnectAsync first.");

        // 模拟设备直接接受写入值，不额外做协议转换
        _logger.LogInformation(
            "Simulator write succeeded: Device={DeviceName}, Tag={Tag}, Value={Value}",
            _currentDevice.Name,
            dataPoint.Tag,
            value);

        return Task.FromResult(value);
    }
}
