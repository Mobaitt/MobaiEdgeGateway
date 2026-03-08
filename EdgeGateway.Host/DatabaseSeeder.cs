using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EdgeGateway.Infrastructure.Data;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Host;

/// <summary>
/// 数据库种子数据服务
/// 在数据库初始化后自动添加测试数据
/// </summary>
public class DatabaseSeeder
{
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        ILogger<DatabaseSeeder> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// 初始化测试数据（如果数据库中没有任何设备）
    /// </summary>
    public async Task InitializeTestDataAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        // 检查是否已有设备，如果有则跳过
        if (await context.Devices.AnyAsync())
        {
            _logger.LogInformation("数据库中已存在设备数据，跳过测试数据初始化");
            return;
        }

        _logger.LogInformation("开始初始化测试数据...");

        try
        {
            // 1. 批量插入 100 个测试设备（全部使用 Modbus 协议，连接地址 127.0.0.1）
            // 端口分配：1-50 使用 502，51-85 使用 503，86-100 使用 504
            var devices = new List<Device>();
            for (int i = 1; i <= 1; i++)
            {
                int port = i switch
                {
                    <= 50 => 502, // 1-50 使用 502 端口（50 个设备）
                    <= 85 => 503, // 51-85 使用 503 端口（35 个设备）
                    _ => 504 // 86-100 使用 504 端口（15 个设备）
                };

                devices.Add(new Device
                {
                    Name = $"Modbus 设备{i:D3}",
                    Code = $"MODBUS_DEVICE_{i:D3}",
                    Description = $"Modbus 测试设备{i:D3} (端口:{port})",
                    Protocol = CollectionProtocol.Modbus,
                    Address = "127.0.0.1",
                    Port = port,
                    PollingIntervalMs = 1000,
                    IsEnabled = true
                });
            }

            await context.Devices.AddRangeAsync(devices);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个测试设备", devices.Count);

            // 2. 批量插入测试数据点（每个设备地址 0-10，Float 类型，2 位寄存器，ABCD 字节序，站 ID 全部为 1）
            var dataPoints = new List<DataPoint>();

            // 数据点名称和单位模板（11 个）
            var pointNames = new[]
                { "温度 01", "温度 02", "温度 03", "压力 01", "压力 02", "湿度", "流量", "液位", "密度", "电导率", "PH 值" };
            var pointUnits = new[] { "℃", "℃", "℃", "MPa", "MPa", "%RH", "m³/h", "m", "kg/m³", "μS/cm", "" };

            // 为每个设备生成 11 个数据点（地址 0-10）
            foreach (var device in devices)
            {
                for (int addr = 0; addr <= 10; addr += 2)
                {
                    dataPoints.Add(new DataPoint
                    {
                        DeviceId = device.Id,
                        Name = pointNames[addr],
                        Tag = $"{device.Code}.Point{addr:D2}",
                        Description = $"传感器点{addr}",
                        Address = addr.ToString(),
                        DataType = DataValueType.Float,
                        Unit = pointUnits[addr],
                        IsEnabled = true,
                        RegisterLength = 2,
                        ModbusSlaveId = 1,
                        ModbusFunctionCode = 3,
                        ModbusByteOrder = ModbusByteOrder.ABCD
                    });
                }
            }

            await context.DataPoints.AddRangeAsync(dataPoints);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个测试数据点", dataPoints.Count);

            // 3. 插入测试虚拟数据点（仅为前 10 个设备生成，每个设备 3 个虚拟点）
            var virtualPoints = new List<VirtualDataPoint>();
            var first10Devices = devices.Take(10).ToList();

            foreach (var device in first10Devices)
            {
                virtualPoints.Add(new VirtualDataPoint
                {
                    DeviceId = device.Id,
                    Name = "平均温度",
                    Tag = $"{device.Code}.Virtual.AvgTemp",
                    Description = "温度平均值",
                    Expression = $"Avg({device.Code}.Point00, {device.Code}.Point02, {device.Code}.Point04)",
                    CalculationType = CalculationType.Average,
                    DataType = DataValueType.Float,
                    Unit = "℃",
                    DependencyTags = $"[\"{device.Code}.Point00\",\"{device.Code}.Point02\",\"{device.Code}.Point04\"]"
                });

                virtualPoints.Add(new VirtualDataPoint
                {
                    DeviceId = device.Id,
                    Name = "温压积",
                    Tag = $"{device.Code}.Virtual.TempPressProd",
                    Description = "温度压力乘积",
                    Expression = $"{device.Code}.Point00 * {device.Code}.Point02",
                    CalculationType = CalculationType.Custom,
                    DataType = DataValueType.Float,
                    Unit = "",
                    DependencyTags = $"[\"{device.Code}.Point00\",\"{device.Code}.Point02\"]"
                });

                virtualPoints.Add(new VirtualDataPoint
                {
                    DeviceId = device.Id,
                    Name = "温差",
                    Tag = $"{device.Code}.Virtual.TempDiff",
                    Description = "温度差值",
                    Expression = $"{device.Code}.Point00 - {device.Code}.Point02",
                    CalculationType = CalculationType.Custom,
                    DataType = DataValueType.Float,
                    Unit = "℃",
                    DependencyTags = $"[\"{device.Code}.Point00\",\"{device.Code}.Point02\"]"
                });
            }

            await context.VirtualDataPoints.AddRangeAsync(virtualPoints);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个测试虚拟数据点", virtualPoints.Count);

            // 4. 插入测试发送通道（仅 HTTP 服务端和 WebSocket）
            var channels = new List<Channel>
            {
                new()
                {
                    Name = "HTTP 数据接口", Code = "HTTP_SERVER", Description = "提供 HTTP 数据接口",
                    Protocol = SendProtocol.Http, Endpoint = "/api/v1/telemetry", IsEnabled = true, HttpMethod = "POST",
                    HttpTimeout = 5000, HttpMode = "server"
                },
                new()
                {
                    Name = "WebSocket 推送", Code = "WS_PUSH", Description = "WebSocket 实时推送",
                    Protocol = SendProtocol.WebSocket, Endpoint = "/ws", IsEnabled = true,
                    WsSubscribeTopic = "edge/telemetry", WsHeartbeatInterval = 30000
                }
            };

            await context.Channels.AddRangeAsync(channels);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个测试发送通道", channels.Count);

            // 5. 插入通道 - 数据点映射关系
            var mappings = new List<ChannelDataPointMapping>();

            // HTTP 服务端通道映射 - 所有设备数据点
            var httpServerChannel = channels.First(c => c.Code == "HTTP_SERVER");
            foreach (var dp in dataPoints)
            {
                // 获取设备编码用于构建完整 Tag
                var device = devices.First(d => d.Id == dp.DeviceId);
                mappings.Add(new ChannelDataPointMapping
                {
                    ChannelId = httpServerChannel.Id,
                    DataPointId = dp.Id,
                    DataPointTag = $"{device.Code}.{dp.Tag}",
                    DataPointName = dp.Name,
                    IsEnabled = true
                });
            }

            // HTTP 服务端通道映射 - 所有虚拟数据点
            foreach (var vp in virtualPoints)
            {
                var device = devices.First(d => d.Id == vp.DeviceId);
                mappings.Add(new ChannelDataPointMapping
                {
                    ChannelId = httpServerChannel.Id,
                    VirtualDataPointId = vp.Id,
                    DataPointTag = $"{device.Code}.{vp.Tag}",
                    DataPointName = vp.Name,
                    IsEnabled = true
                });
            }

            // WebSocket 通道映射 - 所有设备数据点
            var wsChannel = channels.First(c => c.Code == "WS_PUSH");
            foreach (var dp in dataPoints)
            {
                var device = devices.First(d => d.Id == dp.DeviceId);
                mappings.Add(new ChannelDataPointMapping
                {
                    ChannelId = wsChannel.Id,
                    DataPointId = dp.Id,
                    DataPointTag = $"{device.Code}.{dp.Tag}",
                    DataPointName = dp.Name,
                    IsEnabled = true
                });
            }

            // WebSocket 通道映射 - 所有虚拟数据点
            foreach (var vp in virtualPoints)
            {
                var device = devices.First(d => d.Id == vp.DeviceId);
                mappings.Add(new ChannelDataPointMapping
                {
                    ChannelId = wsChannel.Id,
                    VirtualDataPointId = vp.Id,
                    DataPointTag = $"{device.Code}.{vp.Tag}",
                    DataPointName = vp.Name,
                    IsEnabled = true
                });
            }

            await context.ChannelDataPointMappings.AddRangeAsync(mappings);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个通道映射关系", mappings.Count);

            _logger.LogInformation(
                "测试数据初始化完成！设备：{Devices}, 数据点：{DataPoints}, 虚拟点：{VirtualPoints}, 通道：{Channels}, 映射：{Mappings}",
                devices.Count, dataPoints.Count, virtualPoints.Count, channels.Count, mappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试数据初始化失败");
            throw;
        }
    }
}