using System.IO;
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
    // Modbus 应用协议规定单次寄存器读取最多 125 个，线圈读取最多 2000 个。
    private const int MaxRegisterReadCount = 125;
    private const int MaxCoilReadCount = 2000;
    private readonly ILogger<ModbusCollectionStrategy> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private bool _isConnected;
    private string? _connectedAddress;
    private int? _connectedPort;
    private readonly object _planLock = new();
    private string? _planKey;
    private List<ReadGroupPlan>? _readPlan;

    public ModbusCollectionStrategy(ILogger<ModbusCollectionStrategy> logger)
    {
        _logger = logger;
    }

    public string ProtocolName => "Modbus";

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var port = device.Port ?? 502;
            if (_isConnected &&
                string.Equals(_connectedAddress, device.Address, StringComparison.OrdinalIgnoreCase) &&
                _connectedPort == port)
            {
                _logger.LogDebug("Modbus device [{DeviceName}] connection reused", device.Name);
                return;
            }

            DisconnectCore();
            _logger.LogInformation(
                "Connecting Modbus device [{DeviceName}] -> {Address}:{Port}",
                device.Name,
                device.Address,
                port);

            var tcpClient = new TcpClient
            {
                SendTimeout = 5000,
                ReceiveTimeout = 5000
            };

            try
            {
                await tcpClient.ConnectAsync(device.Address, port, cancellationToken);
                _tcpClient = tcpClient;
                _master = ModbusIpMaster.CreateIp(tcpClient);
                _connectedAddress = device.Address;
                _connectedPort = port;
                _isConnected = true;
                _logger.LogInformation("Modbus device [{DeviceName}] connected", device.Name);
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogError(ex, "Failed to connect Modbus device [{DeviceName}]", device.Name);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            DisconnectCore();
            _logger.LogInformation("Modbus connection closed");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        Action<CollectedData> callback,
        CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            await ReadCoreAsync(dataPoints, callback);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task ReadCoreAsync(
        IEnumerable<DataPoint> dataPoints,
        Action<CollectedData> callback)
    {
        if (!_isConnected || _master == null)
            throw new InvalidOperationException("Modbus device is not connected.");

        var dataList = dataPoints.ToList();
        var firstPoint = dataList.FirstOrDefault();
        if (firstPoint == null)
            return;

        // 分组和地址排序计划在点位配置不变时复用。
        var deviceCode = firstPoint.Device?.Code ?? $"Device_{firstPoint.DeviceId}";
        var readPlan = GetReadPlan(dataList);

        foreach (var group in readPlan)
        {
            try
            {
                await ReadGroupAsync(group.Points, group.SlaveId, group.FunctionCode, deviceCode, callback);
            }
            catch (Exception ex)
            {
                if (IsConnectionLevelException(ex))
                {
                    _isConnected = false;
                    throw;
                }

                _logger.LogWarning(
                    ex,
                    "Failed to read Modbus group SlaveId={SlaveId}, FunctionCode={FunctionCode}",
                    group.SlaveId,
                    group.FunctionCode);

                foreach (var dp in group.Points)
                {
                    callback(CreateCollectedData(dp, deviceCode, null));
                }
            }
        }
    }

    public async Task<object?> WriteAsync(DataPoint dataPoint, object? value, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            return await WriteCoreAsync(dataPoint, value);
        }
        catch (Exception ex) when (IsConnectionLevelException(ex))
        {
            _isConnected = false;
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<object?> WriteCoreAsync(DataPoint dataPoint, object? value)
    {
        if (!_isConnected || _master == null)
            throw new InvalidOperationException("Modbus device is not connected.");

        // 先把外部输入归一化成目标点位的数据类型，再按寄存器格式写入
        var slaveId = dataPoint.ModbusSlaveId ?? 1;
        var functionCode = dataPoint.ModbusFunctionCode ?? 3;
        var address = ParseAddress(dataPoint.Address);
        var typedValue = DataPointWriteValueConverter.Normalize(dataPoint, value);

        if (dataPoint.ModbusBitIndex.HasValue)
        {
            if (dataPoint.ModbusBitIndex.Value > 15)
                throw new InvalidOperationException("Modbus bit index must be between 0 and 15.");

            if (functionCode != 3)
                throw new InvalidOperationException("Register bit writes require function code 03.");

            if (typedValue is not bool bitValue)
                throw new InvalidOperationException("Register bit writes only support boolean values.");

            // 位写入采用读-改-写，保留同一寄存器中其他位的当前状态。
            var current = await _master!.ReadHoldingRegistersAsync(slaveId, address, 1);
            var mask = (ushort)(1 << dataPoint.ModbusBitIndex.Value);
            var updated = bitValue
                ? (ushort)(current[0] | mask)
                : (ushort)(current[0] & ~mask);

            await _master.WriteSingleRegisterAsync(slaveId, address, updated);
            return bitValue;
        }

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

    private void DisconnectCore()
    {
        _master?.Dispose();
        _tcpClient?.Dispose();
        _master = null;
        _tcpClient = null;
        _connectedAddress = null;
        _connectedPort = null;
        _isConnected = false;
    }

    private List<ReadGroupPlan> GetReadPlan(List<DataPoint> dataPoints)
    {
        var key = string.Join("|", dataPoints
            .Select(dp => $"{dp.Id}:{dp.Address}:{dp.DataType}:{dp.RegisterLength}:{dp.ModbusSlaveId ?? 1}:{dp.ModbusFunctionCode ?? 3}:{dp.ModbusByteOrder ?? ModbusByteOrder.ABCD}:{dp.ModbusBitIndex?.ToString() ?? "-"}:{dp.Unit ?? "-"}")
            .OrderBy(value => value, StringComparer.Ordinal));

        lock (_planLock)
        {
            if (_readPlan != null && string.Equals(_planKey, key, StringComparison.Ordinal))
                return _readPlan;

            _readPlan = dataPoints
                .GroupBy(dp => new { SlaveId = dp.ModbusSlaveId ?? 1, FunctionCode = dp.ModbusFunctionCode ?? 3 })
                .Select(group => new ReadGroupPlan(
                    group.Key.SlaveId,
                    group.Key.FunctionCode,
                    group.OrderBy(dp => ParseAddress(dp.Address)).ToList()))
                .ToList();
            _planKey = key;
            return _readPlan;
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
        var maxReadCount = functionCode == 1 ? MaxCoilReadCount : MaxRegisterReadCount;
        var addressRanges = MergeContinuousAddresses(sortedPoints, maxReadCount);

        foreach (var range in addressRanges)
        {
            try
            {
                switch (functionCode)
                {
                    case 1:
                    case 2:
                        var bits = functionCode == 1
                            ? await _master!.ReadCoilsAsync(slaveId, range.StartAddress, (ushort)range.Count)
                            : await _master!.ReadInputsAsync(slaveId, range.StartAddress, (ushort)range.Count);
                        foreach (var point in range.Points)
                        {
                            var offset = ParseAddress(point.Address) - range.StartAddress;
                            callback(CreateCollectedData(point, deviceCode, offset < bits.Length ? bits[offset] : null));
                        }
                        continue;

                    case 3:
                    case 4:
                    default:
                var registers = functionCode == 4
                            ? await _master!.ReadInputRegistersAsync(slaveId, range.StartAddress, (ushort)range.Count)
                            : await _master!.ReadHoldingRegistersAsync(slaveId, range.StartAddress, (ushort)range.Count);

                        // 按点位配置的寄存器长度依次解析结果
                        foreach (var point in range.Points)
                        {
                            var registerIndex = ParseAddress(point.Address) - range.StartAddress;
                            var parsedValue = ParseRegisterValue(point, registers, registerIndex);
                            callback(CreateCollectedData(point, deviceCode, parsedValue));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (IsConnectionLevelException(ex))
                {
                    _isConnected = false;
                    throw;
                }

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

    private static bool IsConnectionLevelException(Exception ex)
    {
        if (ex is SocketException or IOException or ObjectDisposedException)
            return true;

        if (ex is InvalidOperationException invalidOperationException)
        {
            var message = invalidOperationException.Message;
            if (!string.IsNullOrWhiteSpace(message) &&
                (message.Contains("non-connected sockets", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("not connected", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return ex.InnerException != null && IsConnectionLevelException(ex.InnerException);
    }

    private static List<AddressRange> MergeContinuousAddresses(List<DataPoint> points, int maxReadCount)
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
            var currentEnd = currentRange.StartAddress + currentRange.Count;
            var registerCount = points[i].RegisterLength;
            var pointEnd = currentAddress + registerCount;

            // 支持同一寄存器的多个位点共用一次读取，同时保留连续地址合并。
            if (currentAddress <= currentEnd && pointEnd - currentRange.StartAddress <= maxReadCount)
            {
                currentRange.Count = Math.Max(currentRange.Count, pointEnd - currentRange.StartAddress);
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

        if (dp.ModbusBitIndex.HasValue)
        {
            if (dp.ModbusBitIndex.Value > 15)
                throw new InvalidOperationException("Modbus bit index must be between 0 and 15.");

            return (registers[index] & (1 << dp.ModbusBitIndex.Value)) != 0;
        }

        if (dp.DataType == DataValueType.Hex)
            return registers[index].ToString("X4");

        if (dp.DataType == DataValueType.Binary)
            return Convert.ToString(registers[index], 2).PadLeft(16, '0');

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

        var bytes = Get64BitBytes(registers, index, byteOrder);

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

    private static byte[] Get64BitBytes(ushort[] registers, int index, ModbusByteOrder byteOrder)
    {
        var wireBytes = new byte[8];
        for (var wordIndex = 0; wordIndex < 4; wordIndex++)
        {
            var register = registers[index + wordIndex];
            wireBytes[wordIndex * 2] = (byte)(register >> 8);
            wireBytes[wordIndex * 2 + 1] = (byte)register;
        }

        // 64 位排列按参考界面中的四个 16 位字处理：
        // ABCD=AB CD EF GH，CDAB=GH EF CD AB，BADC=BA DC FE HG，DCBA=HG FE BA DC。
        var logicalBytes = Reorder64BitWords(wireBytes, byteOrder);
        Array.Reverse(logicalBytes);
        return logicalBytes;
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
            throw new FormatException("Invalid Modbus address: " + addressStr);

        if (raw >= 40000) return (ushort)(raw - 40000);
        if (raw >= 30000) return (ushort)(raw - 30000);
        if (raw >= 10000) return (ushort)(raw - 10000);
        return raw;
    }

    private static ushort[] ConvertToRegisters(DataPoint dataPoint, object value)
    {
        // 把标准 .NET 数值转换为 Modbus 寄存器序列
        return dataPoint.DataType switch
        {
            DataValueType.Bool => [Convert.ToBoolean(value) ? (ushort)1 : (ushort)0],
            DataValueType.Int16 => [(ushort)Convert.ToInt16(value)],
            DataValueType.UInt16 => [Convert.ToUInt16(value)],
            DataValueType.Hex => [Convert.ToUInt16(value)],
            DataValueType.Binary => [Convert.ToUInt16(value)],
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

        if (bytes.Length == 8)
        {
            var logicalBytes = (byte[])bytes.Clone();
            Array.Reverse(logicalBytes);
            return ToRegisters(Reorder64BitWords(logicalBytes, byteOrder));
        }

        // 根据字节序重排寄存器，高低字顺序由点位配置决定
        return byteOrder switch
        {
            ModbusByteOrder.ABCD => [ToRegister(bytes[3], bytes[2]), ToRegister(bytes[1], bytes[0])],
            ModbusByteOrder.CDAB => [ToRegister(bytes[1], bytes[0]), ToRegister(bytes[3], bytes[2])],
            ModbusByteOrder.BADC => [ToRegister(bytes[2], bytes[3]), ToRegister(bytes[0], bytes[1])],
            ModbusByteOrder.DCBA => [ToRegister(bytes[0], bytes[1]), ToRegister(bytes[2], bytes[3])],
            _ => [ToRegister(bytes[3], bytes[2]), ToRegister(bytes[1], bytes[0])]
        };
    }

    private static byte[] Reorder64BitWords(byte[] bytes, ModbusByteOrder byteOrder)
    {
        return byteOrder switch
        {
            ModbusByteOrder.ABCD => [.. bytes],
            ModbusByteOrder.CDAB => [.. bytes[6..8], .. bytes[4..6], .. bytes[2..4], .. bytes[0..2]],
            ModbusByteOrder.BADC => [bytes[1], bytes[0], bytes[3], bytes[2], bytes[5], bytes[4], bytes[7], bytes[6]],
            ModbusByteOrder.DCBA => [bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0]],
            _ => [.. bytes]
        };
    }

    private static ushort[] ToRegisters(byte[] bytes)
    {
        var registers = new ushort[bytes.Length / 2];
        for (var index = 0; index < registers.Length; index++)
            registers[index] = ToRegister(bytes[index * 2], bytes[index * 2 + 1]);

        return registers;
    }

    private static ushort ToRegister(byte high, byte low) => (ushort)((high << 8) | low);

    private sealed record ReadGroupPlan(byte SlaveId, int FunctionCode, List<DataPoint> Points);

    private sealed class AddressRange
    {
        public ushort StartAddress { get; set; }
        public int Count { get; set; }
        public List<DataPoint> Points { get; set; } = [];
    }
}
