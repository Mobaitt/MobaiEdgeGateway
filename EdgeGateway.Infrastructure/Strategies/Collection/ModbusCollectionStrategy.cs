using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

/// <summary>
/// 【采集策略实现】Modbus TCP/RTU 采集策略
/// 
/// 实际项目中可引入 NModbus4 或 FluentModbus NuGet包实现
/// 本文件提供完整的结构框架和注释说明
/// </summary>
public class ModbusCollectionStrategy : ICollectionStrategy
{
    private readonly ILogger<ModbusCollectionStrategy> _logger;

    // 实际项目中此处会持有 ModbusMaster 或 ModbusClient 实例
    // private IModbusMaster? _master;
    private bool _isConnected;

    public ModbusCollectionStrategy(ILogger<ModbusCollectionStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProtocolName => "Modbus";

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    /// <remarks>
    /// 建立Modbus TCP连接到PLC/仪表
    /// 实际实现：new TcpClient(device.Address, device.Port ?? 502) → ModbusFactory.CreateMaster(...)
    /// </remarks>
    public async Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在连接Modbus设备 [{DeviceName}] -> {Address}:{Port}",
            device.Name, device.Address, device.Port ?? 502);

        // TODO: 引入 NModbus4 后取消注释
        // var tcpClient = new TcpClient();
        // await tcpClient.ConnectAsync(device.Address, device.Port ?? 502, cancellationToken);
        // var factory = new ModbusFactory();
        // _master = factory.CreateMaster(tcpClient);

        await Task.Delay(100, cancellationToken); // 模拟连接延迟
        _isConnected = true;
        _logger.LogInformation("Modbus设备 [{DeviceName}] 连接成功", device.Name);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // _master?.Dispose();
        _isConnected = false;
        _logger.LogInformation("Modbus连接已断开");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 根据数据点地址读取Modbus寄存器值
    /// 地址格式约定：40001 → 保持寄存器(Holding Register)，从站号默认1
    /// 实际实现：_master.ReadHoldingRegisters(slaveId, startAddress, numRegisters)
    /// </remarks>
    public async Task<IEnumerable<CollectedData>> ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
            throw new InvalidOperationException("Modbus设备未连接");

        var results = new List<CollectedData>();

        foreach (var dp in dataPoints)
        {
            try
            {
                // 解析地址（如 "40001" → 寄存器0，"00001" → 线圈0）
                var address = ParseAddress(dp.Address);

                // TODO: 实际读取
                // ushort[] registers = await _master.ReadHoldingRegistersAsync(1, address, 2);
                // object value = ConvertRegistersToValue(registers, dp.DataType);

                // 占位：返回0值
                object? value = 0.0f;

                results.Add(new CollectedData
                {
                    Tag = dp.Tag,
                    DataPointId = dp.Id,
                    Value = value,
                    Quality = DataQuality.Good,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取数据点 [{Tag}] 失败", dp.Tag);
                results.Add(new CollectedData
                {
                    Tag = dp.Tag,
                    DataPointId = dp.Id,
                    Value = null,
                    Quality = DataQuality.Bad,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        await Task.CompletedTask;
        return results;
    }

    /// <summary>
    /// 解析Modbus地址字符串（如 "40001" → 寄存器地址0）
    /// </summary>
    private static ushort ParseAddress(string addressStr)
    {
        if (ushort.TryParse(addressStr, out var raw))
        {
            // Modbus地址规范：40001 → 寄存器区，减去区偏移量
            if (raw >= 40001) return (ushort)(raw - 40001);
            if (raw >= 30001) return (ushort)(raw - 30001);
            if (raw >= 10001) return (ushort)(raw - 10001);
            return raw;
        }
        return 0;
    }
}
