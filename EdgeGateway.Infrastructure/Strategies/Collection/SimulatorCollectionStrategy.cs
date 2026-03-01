using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

/// <summary>
/// 【采集策略实现】模拟数据采集策略
/// 不依赖真实硬件，随机生成数据用于本地开发/测试
/// 对应协议类型：Simulator
/// </summary>
public class SimulatorCollectionStrategy : ICollectionStrategy
{
    private readonly ILogger<SimulatorCollectionStrategy> _logger;
    private readonly Random _random = new();

    // 当前连接的设备信息
    private Device? _currentDevice;

    public SimulatorCollectionStrategy(ILogger<SimulatorCollectionStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProtocolName => "Simulator";

    /// <inheritdoc/>
    public bool IsConnected => _currentDevice != null;

    /// <inheritdoc/>
    public Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        _currentDevice = device;
        _logger.LogInformation("模拟采集器已连接设备: {DeviceName}", device.Name);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("模拟采集器已断开设备: {DeviceName}", _currentDevice?.Name);
        _currentDevice = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 对每个数据点生成随机值，模拟真实采集场景
    /// </remarks>
    public Task<IEnumerable<CollectedData>> ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        CancellationToken cancellationToken = default)
    {
        if (_currentDevice == null)
            throw new InvalidOperationException("设备未连接，请先调用 ConnectAsync");

        var results = new List<CollectedData>();

        foreach (var dp in dataPoints)
        {
            // 随机模拟10%的采集失败（测试Bad质量）
            var quality = _random.NextDouble() < 0.1 ? DataQuality.Bad : DataQuality.Good;

            object? value = quality == DataQuality.Bad ? null : dp.DataType switch
            {
                Domain.Enums.DataValueType.Bool   => _random.Next(0, 2) == 1,
                Domain.Enums.DataValueType.Int16  => (short)_random.Next(-1000, 1000),
                Domain.Enums.DataValueType.Int32  => _random.Next(-100000, 100000),
                Domain.Enums.DataValueType.Float  => Math.Round(_random.NextDouble() * 100, 2),
                Domain.Enums.DataValueType.Double => Math.Round(_random.NextDouble() * 1000, 4),
                Domain.Enums.DataValueType.String => $"simulated_{_random.Next(1000)}",
                _ => null
            };

            results.Add(new CollectedData
            {
                Tag         = dp.Tag,
                DataPointId = dp.Id,
                DeviceId    = _currentDevice.Id,
                DeviceName  = _currentDevice.Name,
                Value       = value,
                Quality     = quality,
                Timestamp   = DateTime.UtcNow
            });
        }

        _logger.LogDebug("模拟采集完成，共采集 {Count} 个数据点", results.Count);
        return Task.FromResult<IEnumerable<CollectedData>>(results);
    }
}
