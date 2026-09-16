using Xunit;
using System.Net;
using System.Net.Sockets;
using Modbus.Data;
using Modbus.Device;
using System.Diagnostics;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Infrastructure.Strategies.Collection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdgeGateway.Tests;

public sealed class CapacitySmokeTests
{
    [Fact]
    public async Task Simulator_reads_10000_points_in_one_cycle()
    {
        var strategy = new SimulatorCollectionStrategy(NullLogger<SimulatorCollectionStrategy>.Instance, new SimulatorValueStore());
        var device = new Device
        {
            Id = 9001,
            Code = "capacity-device",
            Name = "Capacity Device",
            Protocol = CollectionProtocol.Simulator,
            IsEnabled = true
        };

        var points = Enumerable.Range(0, 10_000)
            .Select(index => new DataPoint
            {
                Id = index + 1,
                DeviceId = device.Id,
                Device = device,
                Name = $"Point {index}",
                Tag = $"capacity.point.{index}",
                Address = index.ToString(),
                DataType = DataValueType.Float,
                RegisterLength = 2,
                IsEnabled = true
            })
            .ToList();

        await strategy.ConnectAsync(device);
        try
        {
            var count = 0;
            var stopwatch = Stopwatch.StartNew();

            await strategy.ReadAsync(points, _ => Interlocked.Increment(ref count));

            stopwatch.Stop();
            Assert.Equal(10_000, count);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"10,000 点 Simulator 采集耗时 {stopwatch.ElapsedMilliseconds}ms，超过 5 秒上限。");
        }
        finally
        {
            await strategy.DisconnectAsync();
        }
    }
    [Fact]
    public async Task ModbusTcp_reads_10000_float_points_in_protocol_limited_batches()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var slave = ModbusTcpSlave.CreateTcp(1, listener);
        slave.DataStore = DataStoreFactory.CreateDefaultDataStore(0, 0, 20_001, 0);

        for (var index = 0; index < 20_000; index++)
            slave.DataStore.HoldingRegisters[index + 1] = (ushort)(index % ushort.MaxValue);

        var listenTask = Task.Run(slave.Listen);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var strategy = new ModbusCollectionStrategy(NullLogger<ModbusCollectionStrategy>.Instance);
        var device = new Device
        {
            Id = 9002,
            Code = "modbus-capacity-device",
            Name = "Modbus Capacity Device",
            Protocol = CollectionProtocol.Modbus,
            Address = IPAddress.Loopback.ToString(),
            Port = port,
            IsEnabled = true
        };

        var points = Enumerable.Range(0, 10_000)
            .Select(index => new DataPoint
            {
                Id = index + 1,
                DeviceId = device.Id,
                Device = device,
                Name = $"Point {index}",
                Tag = $"modbus.capacity.point.{index}",
                Address = (index * 2).ToString(),
                DataType = DataValueType.Float,
                RegisterLength = 2,
                ModbusSlaveId = 1,
                ModbusFunctionCode = 3,
                IsEnabled = true
            })
            .ToList();

        await strategy.ConnectAsync(device);
        try
        {
            var count = 0;
            var stopwatch = Stopwatch.StartNew();
            await strategy.ReadAsync(points, _ => Interlocked.Increment(ref count));
            stopwatch.Stop();

            Assert.Equal(10_000, count);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15),
                $"10,000 点 Modbus TCP 采集耗时 {stopwatch.ElapsedMilliseconds}ms，超过 15 秒上限。");
        }
        finally
        {
            await strategy.DisconnectAsync();
            slave.Dispose();
            listener.Stop();
            await Task.WhenAny(listenTask, Task.Delay(TimeSpan.FromSeconds(2)));
        }
    }
}