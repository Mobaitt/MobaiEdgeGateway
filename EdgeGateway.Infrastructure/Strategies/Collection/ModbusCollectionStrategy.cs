using System.Net.Sockets;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Modbus.Device;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

/// <summary>
/// 【采集策略实现】Modbus TCP/RTU 采集策略
/// 使用 NModbus4 库实现 Modbus 通信
/// </summary>
public class ModbusCollectionStrategy : ICollectionStrategy
{
    private readonly ILogger<ModbusCollectionStrategy> _logger;
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
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
    public async Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在连接 Modbus 设备 [{DeviceName}] -> {Address}:{Port}",
            device.Name, device.Address, device.Port ?? 502);

        try
        {
            _tcpClient = new TcpClient();
            
            // 设置超时
            _tcpClient.SendTimeout = 5000;
            _tcpClient.ReceiveTimeout = 5000;
            
            await _tcpClient.ConnectAsync(device.Address, device.Port ?? 502, cancellationToken);
            
            // 创建 Modbus Master
            _master = ModbusIpMaster.CreateIp(_tcpClient);
            
            // 验证连接（读取保持寄存器测试）
            await _master.ReadHoldingRegistersAsync(1, 0, 1);
            
            _isConnected = true;
            _logger.LogInformation("Modbus 设备 [{DeviceName}] 连接成功", device.Name);
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogError(ex, "Modbus 设备 [{DeviceName}] 连接失败", device.Name);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _master?.Dispose();
        _tcpClient?.Dispose();
        _isConnected = false;
        _logger.LogInformation("Modbus 连接已断开");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CollectedData>> ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        Action<CollectedData> callback,
        CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _master == null)
            throw new InvalidOperationException("Modbus 设备未连接");

        var results = new List<CollectedData>();
        var dataList = dataPoints.ToList();
        
        // 获取设备信息（从第一个数据点获取设备信息）
        var firstPoint = dataList.FirstOrDefault();
        if (firstPoint == null)
        {
            return results;
        }

        // 使用设备编码作为标识
        var deviceCode = firstPoint.Device?.Code ?? $"Device_{firstPoint.DeviceId}";

        // 按从站地址和功能码分组，减少通信次数
        var groupedPoints = dataList.GroupBy(dp => new
        {
            SlaveId = dp.ModbusSlaveId ?? 1,
            FunctionCode = dp.ModbusFunctionCode ?? 3
        });

        foreach (var group in groupedPoints)
        {
            try
            {
                var groupResults = await ReadGroupAsync(group, deviceCode, cancellationToken);
                results.AddRange(groupResults);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取从站{SlaveId}功能码{FuncCode}失败",
                    group.Key.SlaveId, group.Key.FunctionCode);

                // 为该组所有数据点返回错误结果
                foreach (var dp in group)
                {
                    results.Add(CreateCollectedData(dp, deviceCode, null));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 读取一组数据点（同一从站、同一功能码）
    /// </summary>
    private async Task<List<CollectedData>> ReadGroupAsync(
        IGrouping<dynamic, DataPoint> group,
        string deviceCode,
        CancellationToken cancellationToken)
    {
        var results = new List<CollectedData>();
        var slaveId = group.Key.SlaveId;
        var functionCode = group.Key.FunctionCode;

        // 按地址排序并合并连续地址
        var sortedPoints = group.OrderBy(dp => ParseAddress(dp.Address)).ToList();
        var addressRanges = MergeContinuousAddresses(sortedPoints);

        foreach (var range in addressRanges)
        {
            try
            {
                ushort[] registers;

                // 根据功能码选择读取方法
                switch (functionCode)
                {
                    case 1: // 线圈
                        var coils = await _master!.ReadCoilsAsync(slaveId, range.StartAddress, (ushort)range.Count);
                        for (int i = 0; i < range.Points.Count; i++)
                        {
                            var dp = range.Points[i];
                            results.Add(CreateCollectedData(dp, deviceCode, coils[i] ? 1 : 0));
                        }
                        continue;

                    case 2: // 离散输入 - NModbus4 不支持，跳过
                        _logger.LogWarning("NModbus4 不支持功能码 2 (离散输入)，跳过读取");
                        foreach (var dp in group)
                        {
                            results.Add(CreateCollectedData(dp, deviceCode, null));
                        }
                        continue;

                    case 3: // 保持寄存器
                        registers = await _master.ReadHoldingRegistersAsync(slaveId, range.StartAddress, (ushort)range.Count);
                        break;

                    case 4: // 输入寄存器
                        registers = await _master.ReadInputRegistersAsync(slaveId, range.StartAddress, (ushort)range.Count);
                        break;

                    default:
                        registers = await _master.ReadHoldingRegistersAsync(slaveId, range.StartAddress, (ushort)range.Count);
                        break;
                }

                // 解析寄存器数据
                // 需要跟踪当前读取到的寄存器位置
                int registerIndex = 0;
                for (int i = 0; i < range.Points.Count; i++)
                {
                    var dp = range.Points[i];
                    var value = ParseRegisterValue(dp, registers, registerIndex);
                    results.Add(CreateCollectedData(dp, deviceCode, value));

                    // 累加寄存器索引
                    registerIndex += dp.RegisterLength;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取地址范围 [{StartAddress}-{EndAddress}] 失败",
                    range.StartAddress, range.StartAddress + range.Count - 1);

                foreach (var dp in range.Points)
                {
                    results.Add(CreateCollectedData(dp, deviceCode, null));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 合并连续的寄存器地址，减少 Modbus 请求次数
    /// </summary>
    private static List<AddressRange> MergeContinuousAddresses(List<DataPoint> points)
    {
        var ranges = new List<AddressRange>();

        if (points.Count == 0) return ranges;

        var firstPoint = points[0];
        var currentRange = new AddressRange
        {
            StartAddress = ParseAddress(firstPoint.Address),
            Count = firstPoint.RegisterLength,
            Points = new List<DataPoint> { firstPoint }
        };

        for (int i = 1; i < points.Count; i++)
        {
            var currentAddress = ParseAddress(points[i].Address);
            var expectedNext = currentRange.StartAddress + (ushort)currentRange.Count;

            // 使用数据点配置的寄存器长度
            var registerCount = points[i].RegisterLength;

            if (currentAddress == expectedNext)
            {
                currentRange.Count += registerCount;
                currentRange.Points.Add(points[i]);
            }
            else
            {
                ranges.Add(currentRange);
                currentRange = new AddressRange
                {
                    StartAddress = currentAddress,
                    Count = registerCount,
                    Points = new List<DataPoint> { points[i] }
                };
            }
        }

        ranges.Add(currentRange);
        return ranges;
    }

    /// <summary>
    /// 解析寄存器值为实际数据
    /// </summary>
    private object? ParseRegisterValue(DataPoint dp, ushort[] registers, int index)
    {
        if (index >= registers.Length) return null;

        // 根据寄存器长度读取数据
        return dp.RegisterLength switch
        {
            1 => Parse16BitValue(dp.DataType, registers, index),
            2 => Parse32BitValue(dp.DataType, registers, index, dp.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            4 => Parse64BitValue(dp.DataType, registers, index, dp.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            _ => Parse16BitValue(dp.DataType, registers, index)
        };
    }

    /// <summary>
    /// 解析 16 位值
    /// </summary>
    private static object? Parse16BitValue(DataValueType dataType, ushort[] registers, int index)
    {
        if (index >= registers.Length) return null;

        return dataType switch
        {
            DataValueType.Bool => registers[index] != 0,
            DataValueType.Int16 => (short)registers[index],
            DataValueType.UInt16 => registers[index],
            _ => registers[index]
        };
    }

    /// <summary>
    /// 解析 32 位值
    /// </summary>
    private static object? Parse32BitValue(DataValueType dataType, ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 1 >= registers.Length) return null;

        return dataType switch
        {
            DataValueType.Int32 => ConvertToInt32(registers, index, byteOrder),
            DataValueType.UInt32 => ConvertToUInt32(registers, index, byteOrder),
            DataValueType.Float => ConvertToFloat(registers, index, byteOrder),
            _ => ConvertToInt32(registers, index, byteOrder)
        };
    }

    /// <summary>
    /// 解析 64 位值
    /// </summary>
    private static object? Parse64BitValue(DataValueType dataType, ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 3 >= registers.Length) return null;

        return dataType switch
        {
            DataValueType.Int64 => ConvertToInt64(registers, index, byteOrder),
            DataValueType.UInt64 => ConvertToUInt64(registers, index, byteOrder),
            DataValueType.Double => ConvertToDouble(registers, index, byteOrder),
            _ => ConvertToInt64(registers, index, byteOrder)
        };
    }

    /// <summary>
    /// 根据字节序转换 32 位整数
    /// </summary>
    private static int ConvertToInt32(ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 1 >= registers.Length) return 0;
        
        var bytes = GetBytes(registers[index], registers[index + 1], byteOrder);
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// 根据字节序转换无符号 32 位整数
    /// </summary>
    private static uint ConvertToUInt32(ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 1 >= registers.Length) return 0;
        
        var bytes = GetBytes(registers[index], registers[index + 1], byteOrder);
        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// 根据字节序转换浮点数
    /// </summary>
    private static float ConvertToFloat(ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 1 >= registers.Length) return 0;
        
        var bytes = GetBytes(registers[index], registers[index + 1], byteOrder);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// 根据字节序转换 64 位整数
    /// </summary>
    private static long ConvertToInt64(ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 3 >= registers.Length) return 0;
        
        var allBytes = new List<byte>();
        allBytes.AddRange(GetBytes(registers[index], registers[index + 1], byteOrder));
        allBytes.AddRange(GetBytes(registers[index + 2], registers[index + 3], byteOrder));
        return BitConverter.ToInt64(allBytes.ToArray(), 0);
    }

    /// <summary>
    /// 根据字节序转换无符号 64 位整数
    /// </summary>
    private static ulong ConvertToUInt64(ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 3 >= registers.Length) return 0;
        
        var allBytes = new List<byte>();
        allBytes.AddRange(GetBytes(registers[index], registers[index + 1], byteOrder));
        allBytes.AddRange(GetBytes(registers[index + 2], registers[index + 3], byteOrder));
        return BitConverter.ToUInt64(allBytes.ToArray(), 0);
    }

    /// <summary>
    /// 根据字节序转换双精度浮点数
    /// </summary>
    private static double ConvertToDouble(ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 3 >= registers.Length) return 0;
        
        var allBytes = new List<byte>();
        allBytes.AddRange(GetBytes(registers[index], registers[index + 1], byteOrder));
        allBytes.AddRange(GetBytes(registers[index + 2], registers[index + 3], byteOrder));
        return BitConverter.ToDouble(allBytes.ToArray(), 0);
    }

    /// <summary>
    /// 根据字节序将两个 16 位寄存器转换为 4 字节数组
    /// </summary>
    private static byte[] GetBytes(ushort highWord, ushort lowWord, ModbusByteOrder byteOrder)
    {
        var highBytes = BitConverter.GetBytes(highWord);
        var lowBytes = BitConverter.GetBytes(lowWord);
        
        return byteOrder switch
        {
            ModbusByteOrder.ABCD => [lowBytes[0], lowBytes[1], highBytes[0], highBytes[1]],
            ModbusByteOrder.CDAB => [highBytes[0], highBytes[1], lowBytes[0], lowBytes[1]],
            ModbusByteOrder.BADC => [lowBytes[1], lowBytes[0], highBytes[1], highBytes[0]],
            ModbusByteOrder.DCBA => [highBytes[1], highBytes[0], lowBytes[1], lowBytes[0]],
            _ => [highBytes[0], highBytes[1], lowBytes[0], lowBytes[1]]
        };
    }

    /// <summary>
    /// 创建采集数据对象
    /// </summary>
    private static CollectedData CreateCollectedData(DataPoint dp, string deviceCode, object? value)
    {
        return new CollectedData
        {
            Tag = dp.Tag,  // 设备编码。数据点 Tag = 全局唯一 Tag
            DataPointId = dp.Id,
            DeviceId = dp.DeviceId,
            DeviceName = deviceCode,
            Value = value,
            Unit = dp.Unit,
            Quality = value != null ? DataQuality.Good : DataQuality.Bad,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 解析 Modbus 地址字符串
    /// </summary>
    private static ushort ParseAddress(string addressStr)
    {
        if (ushort.TryParse(addressStr, out var raw))
        {
            // Modbus 地址规范：减去区偏移量
            if (raw >= 40000) return (ushort)(raw - 40000);  // 保持寄存器
            if (raw >= 30000) return (ushort)(raw - 30000);  // 输入寄存器
            if (raw >= 10000) return (ushort)(raw - 10000);  // 线圈
            return raw;  // 离散输入
        }
        return 0;
    }

    /// <summary>
    /// 判断是否为 32 位数据类型
    /// </summary>
    private static bool Is32BitType(DataValueType dataType)
    {
        return dataType is DataValueType.Int32 or DataValueType.UInt32 or DataValueType.Float;
    }

    /// <summary>
    /// 地址范围类，用于合并连续地址
    /// </summary>
    private class AddressRange
    {
        public ushort StartAddress { get; set; }
        public int Count { get; set; }
        public List<DataPoint> Points { get; set; } = new();
    }
}
