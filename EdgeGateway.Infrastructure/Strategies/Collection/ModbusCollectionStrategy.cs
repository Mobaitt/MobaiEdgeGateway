using System.Net.Sockets;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Modbus.Device;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

/// <summary>
/// Modbus TCP 采集与写入策略。
/// 统一处理寄存器读取、类型转换以及点位写入。
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

    public string ProtocolName => "Modbus";

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Connecting Modbus device [{DeviceName}] -> {Address}:{Port}",
            device.Name,
            device.Address,
            device.Port ?? 502);

        try
        {
            _tcpClient = new TcpClient
            {
                SendTimeout = 5000,
                ReceiveTimeout = 5000
            };

            await _tcpClient.ConnectAsync(device.Address, device.Port ?? 502, cancellationToken);
            _master = ModbusIpMaster.CreateIp(_tcpClient);

            // 启动时做一次最小读取，验证连接可用
            await _master.ReadHoldingRegistersAsync(1, 0, 1);

            _isConnected = true;
            _logger.LogInformation("Modbus device [{DeviceName}] connected", device.Name);
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogError(ex, "Failed to connect Modbus device [{DeviceName}]", device.Name);
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _master?.Dispose();
        _tcpClient?.Dispose();
        _isConnected = false;
        _logger.LogInformation("Modbus connection closed");
        return Task.CompletedTask;
    }

    public async Task ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        Action<CollectedData> callback,
        CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _master == null)
            throw new InvalidOperationException("Modbus device is not connected.");

        var dataList = dataPoints.ToList();
        var firstPoint = dataList.FirstOrDefault();
        if (firstPoint == null)
            return;

        // 按从站和功能码分组，减少无效请求次数
        var deviceCode = firstPoint.Device?.Code ?? $"Device_{firstPoint.DeviceId}";

        var groupedPoints = dataList.GroupBy(dp => new
        {
            SlaveId = dp.ModbusSlaveId ?? 1,
            FunctionCode = dp.ModbusFunctionCode ?? 3
        });

        foreach (var group in groupedPoints)
        {
            try
            {
                await ReadGroupAsync(group.ToList(), group.Key.SlaveId, group.Key.FunctionCode, deviceCode, callback);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to read Modbus group SlaveId={SlaveId}, FunctionCode={FunctionCode}",
                    group.Key.SlaveId,
                    group.Key.FunctionCode);

                foreach (var dp in group)
                {
                    callback(CreateCollectedData(dp, deviceCode, null));
                }
            }
        }
    }

    public async Task<object?> WriteAsync(DataPoint dataPoint, object? value, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _master == null)
            throw new InvalidOperationException("Modbus device is not connected.");

        // 先把外部输入归一化成目标点位的数据类型，再按寄存器格式写入
        var slaveId = dataPoint.ModbusSlaveId ?? 1;
        var functionCode = dataPoint.ModbusFunctionCode ?? 3;
        var address = ParseAddress(dataPoint.Address);
        var typedValue = NormalizeWriteValue(dataPoint, value);

        switch (functionCode)
        {
            case 1:
                if (typedValue is not bool coilValue)
                    throw new InvalidOperationException("Coil writes only support boolean values.");

                await _master.WriteSingleCoilAsync(slaveId, address, coilValue);
                return coilValue;

            case 2:
            case 4:
                throw new InvalidOperationException($"Function code {functionCode} is read-only.");

            case 3:
            default:
                var registers = ConvertToRegisters(dataPoint, typedValue);
                if (registers.Length == 1)
                {
                    await _master.WriteSingleRegisterAsync(slaveId, address, registers[0]);
                }
                else
                {
                    await _master.WriteMultipleRegistersAsync(slaveId, address, registers);
                }

                return typedValue;
        }
    }

    private async Task ReadGroupAsync(
        List<DataPoint> points,
        byte slaveId,
        int functionCode,
        string deviceCode,
        Action<CollectedData> callback)
    {
        // 将连续地址合并成批量读取区间，降低通信开销
        var sortedPoints = points.OrderBy(dp => ParseAddress(dp.Address)).ToList();
        var addressRanges = MergeContinuousAddresses(sortedPoints);

        foreach (var range in addressRanges)
        {
            try
            {
                switch (functionCode)
                {
                    case 1:
                        var coils = await _master!.ReadCoilsAsync(slaveId, range.StartAddress, (ushort)range.Count);
                        for (var i = 0; i < range.Points.Count; i++)
                        {
                            callback(CreateCollectedData(range.Points[i], deviceCode, coils[i]));
                        }
                        continue;

                    case 2:
                        throw new InvalidOperationException("Discrete input reads are not supported by this strategy.");

                    case 3:
                    case 4:
                    default:
                        var registers = functionCode == 4
                            ? await _master!.ReadInputRegistersAsync(slaveId, range.StartAddress, (ushort)range.Count)
                            : await _master!.ReadHoldingRegistersAsync(slaveId, range.StartAddress, (ushort)range.Count);

                        // 按点位配置的寄存器长度依次解析结果
                        var registerIndex = 0;
                        foreach (var point in range.Points)
                        {
                            var parsedValue = ParseRegisterValue(point, registers, registerIndex);
                            callback(CreateCollectedData(point, deviceCode, parsedValue));
                            registerIndex += point.RegisterLength;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to read range [{StartAddress}-{EndAddress}]",
                    range.StartAddress,
                    range.StartAddress + range.Count - 1);

                foreach (var point in range.Points)
                {
                    callback(CreateCollectedData(point, deviceCode, null));
                }
            }
        }
    }

    private static List<AddressRange> MergeContinuousAddresses(List<DataPoint> points)
    {
        var ranges = new List<AddressRange>();
        if (points.Count == 0) return ranges;

        var firstPoint = points[0];
        var currentRange = new AddressRange
        {
            StartAddress = ParseAddress(firstPoint.Address),
            Count = firstPoint.RegisterLength,
            Points = [firstPoint]
        };

        for (var i = 1; i < points.Count; i++)
        {
            var currentAddress = ParseAddress(points[i].Address);
            var expectedNext = currentRange.StartAddress + (ushort)currentRange.Count;
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
                    Points = [points[i]]
                };
            }
        }

        ranges.Add(currentRange);
        return ranges;
    }

    private static object? ParseRegisterValue(DataPoint dp, ushort[] registers, int index)
    {
        if (index >= registers.Length) return null;

        return dp.RegisterLength switch
        {
            1 => Parse16BitValue(dp.DataType, registers, index),
            2 => Parse32BitValue(dp.DataType, registers, index, dp.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            4 => Parse64BitValue(dp.DataType, registers, index, dp.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            _ => Parse16BitValue(dp.DataType, registers, index)
        };
    }

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

    private static object? Parse32BitValue(DataValueType dataType, ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 1 >= registers.Length) return null;

        return dataType switch
        {
            DataValueType.Int32 => BitConverter.ToInt32(GetBytes(registers[index], registers[index + 1], byteOrder), 0),
            DataValueType.UInt32 => BitConverter.ToUInt32(GetBytes(registers[index], registers[index + 1], byteOrder), 0),
            DataValueType.Float => BitConverter.ToSingle(GetBytes(registers[index], registers[index + 1], byteOrder), 0),
            _ => BitConverter.ToInt32(GetBytes(registers[index], registers[index + 1], byteOrder), 0)
        };
    }

    private static object? Parse64BitValue(DataValueType dataType, ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        if (index + 3 >= registers.Length) return null;

        var allBytes = new List<byte>();
        allBytes.AddRange(GetBytes(registers[index], registers[index + 1], byteOrder));
        allBytes.AddRange(GetBytes(registers[index + 2], registers[index + 3], byteOrder));
        var bytes = allBytes.ToArray();

        return dataType switch
        {
            DataValueType.Int64 => BitConverter.ToInt64(bytes, 0),
            DataValueType.UInt64 => BitConverter.ToUInt64(bytes, 0),
            DataValueType.Double => BitConverter.ToDouble(bytes, 0),
            _ => BitConverter.ToInt64(bytes, 0)
        };
    }

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

    private static CollectedData CreateCollectedData(DataPoint dp, string deviceCode, object? value)
    {
        return new CollectedData
        {
            Tag = dp.Tag,
            DataPointId = dp.Id,
            DeviceId = dp.DeviceId,
            DeviceName = deviceCode,
            Value = value,
            Unit = dp.Unit,
            Quality = value != null ? DataQuality.Good : DataQuality.Bad,
            Timestamp = DateTime.UtcNow
        };
    }

    private static ushort ParseAddress(string addressStr)
    {
        if (!ushort.TryParse(addressStr, out var raw))
            return 0;

        if (raw >= 40000) return (ushort)(raw - 40000);
        if (raw >= 30000) return (ushort)(raw - 30000);
        if (raw >= 10000) return (ushort)(raw - 10000);
        return raw;
    }

    private static object NormalizeWriteValue(DataPoint dataPoint, object? value)
    {
        if (value == null)
            throw new InvalidOperationException("Write value cannot be null.");

        // 统一把请求体里的值转换成点位配置的数据类型
        return dataPoint.DataType switch
        {
            DataValueType.Bool => value switch
            {
                bool boolValue => boolValue,
                string stringValue when bool.TryParse(stringValue, out var parsedBool) => parsedBool,
                string stringValue when stringValue == "1" => true,
                string stringValue when stringValue == "0" => false,
                _ => throw new InvalidOperationException("Boolean writes only support true/false or 1/0.")
            },
            DataValueType.Int16 => Convert.ToInt16(value),
            DataValueType.UInt16 => Convert.ToUInt16(value),
            DataValueType.Int32 => Convert.ToInt32(value),
            DataValueType.UInt32 => Convert.ToUInt32(value),
            DataValueType.Float => Convert.ToSingle(value),
            DataValueType.Int64 => Convert.ToInt64(value),
            DataValueType.UInt64 => Convert.ToUInt64(value),
            DataValueType.Double => Convert.ToDouble(value),
            DataValueType.String => value.ToString() ?? string.Empty,
            _ => throw new InvalidOperationException($"Unsupported write type: {dataPoint.DataType}")
        };
    }

    private static ushort[] ConvertToRegisters(DataPoint dataPoint, object value)
    {
        // 把标准 .NET 数值转换为 Modbus 寄存器序列
        return dataPoint.DataType switch
        {
            DataValueType.Bool => [Convert.ToBoolean(value) ? (ushort)1 : (ushort)0],
            DataValueType.Int16 => [(ushort)Convert.ToInt16(value)],
            DataValueType.UInt16 => [Convert.ToUInt16(value)],
            DataValueType.Int32 => FromBytes(BitConverter.GetBytes(Convert.ToInt32(value)), dataPoint.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            DataValueType.UInt32 => FromBytes(BitConverter.GetBytes(Convert.ToUInt32(value)), dataPoint.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            DataValueType.Float => FromBytes(BitConverter.GetBytes(Convert.ToSingle(value)), dataPoint.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            DataValueType.Int64 => FromBytes(BitConverter.GetBytes(Convert.ToInt64(value)), dataPoint.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            DataValueType.UInt64 => FromBytes(BitConverter.GetBytes(Convert.ToUInt64(value)), dataPoint.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            DataValueType.Double => FromBytes(BitConverter.GetBytes(Convert.ToDouble(value)), dataPoint.ModbusByteOrder ?? ModbusByteOrder.ABCD),
            _ => throw new InvalidOperationException($"Unsupported write type: {dataPoint.DataType}")
        };
    }

    private static ushort[] FromBytes(byte[] bytes, ModbusByteOrder byteOrder)
    {
        if (bytes.Length != 4 && bytes.Length != 8)
            throw new InvalidOperationException("Only 32-bit and 64-bit values can be converted to Modbus registers.");

        // 根据字节序重排寄存器，高低字顺序由点位配置决定
        if (bytes.Length == 4)
        {
            return byteOrder switch
            {
                ModbusByteOrder.ABCD => [ToRegister(bytes[3], bytes[2]), ToRegister(bytes[1], bytes[0])],
                ModbusByteOrder.CDAB => [ToRegister(bytes[1], bytes[0]), ToRegister(bytes[3], bytes[2])],
                ModbusByteOrder.BADC => [ToRegister(bytes[2], bytes[3]), ToRegister(bytes[0], bytes[1])],
                ModbusByteOrder.DCBA => [ToRegister(bytes[0], bytes[1]), ToRegister(bytes[2], bytes[3])],
                _ => [ToRegister(bytes[3], bytes[2]), ToRegister(bytes[1], bytes[0])]
            };
        }

        var high = FromBytes(bytes[..4], byteOrder);
        var low = FromBytes(bytes[4..], byteOrder);
        return [.. high, .. low];
    }

    private static ushort ToRegister(byte high, byte low) => (ushort)((high << 8) | low);

    private sealed class AddressRange
    {
        public ushort StartAddress { get; set; }
        public int Count { get; set; }
        public List<DataPoint> Points { get; set; } = [];
    }
}
