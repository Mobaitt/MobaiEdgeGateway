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
            // 1. 插入测试设备（全部使用 Simulator 协议）
            var devices = new List<Device>
            {
                new() { Name = "模拟设备 001", Code = "SIM_DEVICE_001", Description = "温度压力模拟设备", Protocol = CollectionProtocol.Simulator, Address = "127.0.0.1", Port = 0, PollingIntervalMs = 1000, IsEnabled = true },
                new() { Name = "模拟设备 002", Code = "SIM_DEVICE_002", Description = "液位流量模拟设备", Protocol = CollectionProtocol.Simulator, Address = "127.0.0.1", Port = 0, PollingIntervalMs = 1000, IsEnabled = true },
                new() { Name = "模拟设备 003", Code = "SIM_DEVICE_003", Description = "环境监测模拟设备", Protocol = CollectionProtocol.Simulator, Address = "127.0.0.1", Port = 0, PollingIntervalMs = 1000, IsEnabled = true },
                new() { Name = "产线模拟设备", Code = "SIM_PROD_LINE", Description = "产线监测模拟设备", Protocol = CollectionProtocol.Simulator, Address = "127.0.0.1", Port = 0, PollingIntervalMs = 2000, IsEnabled = true },
                new() { Name = "动力站模拟设备", Code = "SIM_POWER", Description = "动力站监测模拟设备", Protocol = CollectionProtocol.Simulator, Address = "127.0.0.1", Port = 0, PollingIntervalMs = 3000, IsEnabled = true }
            };

            await context.Devices.AddRangeAsync(devices);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个测试设备", devices.Count);

            // 2. 插入测试数据点（同一设备下 Tag 唯一，采集时会自动拼接为 {deviceCode}.{Tag}）
            var dataPoints = new List<DataPoint>();

            // 设备 1 数据点（温度、压力）
            var device1 = devices.First(d => d.Code == "SIM_DEVICE_001");
            dataPoints.AddRange(new[]
            {
                new DataPoint { DeviceId = device1.Id, Name = "温度 01", Tag = "Temperature01", Description = "主温度传感器", Address = "40001", DataType = DataValueType.Float, Unit = "℃" },
                new DataPoint { DeviceId = device1.Id, Name = "温度 02", Tag = "Temperature02", Description = "辅助温度传感器", Address = "40003", DataType = DataValueType.Float, Unit = "℃" },
                new DataPoint { DeviceId = device1.Id, Name = "温度 03", Tag = "Temperature03", Description = "环境温度", Address = "40005", DataType = DataValueType.Float, Unit = "℃" },
                new DataPoint { DeviceId = device1.Id, Name = "压力 01", Tag = "Pressure01", Description = "主压力传感器", Address = "40007", DataType = DataValueType.Float, Unit = "MPa" },
                new DataPoint { DeviceId = device1.Id, Name = "压力 02", Tag = "Pressure02", Description = "辅助压力", Address = "40009", DataType = DataValueType.Float, Unit = "MPa" },
                new DataPoint { DeviceId = device1.Id, Name = "湿度", Tag = "Humidity", Description = "环境湿度", Address = "40011", DataType = DataValueType.Float, Unit = "%RH" }
            });

            // 设备 2 数据点（液位、流量）
            var device2 = devices.First(d => d.Code == "SIM_DEVICE_002");
            dataPoints.AddRange(new[]
            {
                new DataPoint { DeviceId = device2.Id, Name = "液位 01", Tag = "Level01", Description = "主液位传感器", Address = "40001", DataType = DataValueType.Float, Unit = "m" },
                new DataPoint { DeviceId = device2.Id, Name = "液位 02", Tag = "Level02", Description = "备用液位", Address = "40003", DataType = DataValueType.Float, Unit = "m" },
                new DataPoint { DeviceId = device2.Id, Name = "流量 01", Tag = "Flow01", Description = "入口流量", Address = "40005", DataType = DataValueType.Float, Unit = "m³/h" },
                new DataPoint { DeviceId = device2.Id, Name = "流量 02", Tag = "Flow02", Description = "出口流量", Address = "40007", DataType = DataValueType.Float, Unit = "m³/h" },
                new DataPoint { DeviceId = device2.Id, Name = "温度", Tag = "Temperature", Description = "介质温度", Address = "40009", DataType = DataValueType.Float, Unit = "℃" },
                new DataPoint { DeviceId = device2.Id, Name = "密度", Tag = "Density", Description = "介质密度", Address = "40011", DataType = DataValueType.Float, Unit = "kg/m³" }
            });

            // 设备 3 数据点（环境监测）
            var device3 = devices.First(d => d.Code == "SIM_DEVICE_003");
            dataPoints.AddRange(new[]
            {
                new DataPoint { DeviceId = device3.Id, Name = "PM2.5", Tag = "PM25", Description = "PM2.5 浓度", Address = "40001", DataType = DataValueType.Float, Unit = "μg/m³" },
                new DataPoint { DeviceId = device3.Id, Name = "PM10", Tag = "PM10", Description = "PM10 浓度", Address = "40003", DataType = DataValueType.Float, Unit = "μg/m³" },
                new DataPoint { DeviceId = device3.Id, Name = "CO2", Tag = "CO2", Description = "二氧化碳浓度", Address = "40005", DataType = DataValueType.Float, Unit = "ppm" },
                new DataPoint { DeviceId = device3.Id, Name = "TVOC", Tag = "TVOC", Description = "总挥发性有机物", Address = "40007", DataType = DataValueType.Float, Unit = "ppb" },
                new DataPoint { DeviceId = device3.Id, Name = "温度", Tag = "Temperature", Description = "环境温度", Address = "40009", DataType = DataValueType.Float, Unit = "℃" },
                new DataPoint { DeviceId = device3.Id, Name = "湿度", Tag = "Humidity", Description = "环境湿度", Address = "40011", DataType = DataValueType.Float, Unit = "%RH" },
                new DataPoint { DeviceId = device3.Id, Name = "大气压", Tag = "Pressure", Description = "大气压力", Address = "40013", DataType = DataValueType.Float, Unit = "hPa" },
                new DataPoint { DeviceId = device3.Id, Name = "噪声", Tag = "Noise", Description = "噪声分贝", Address = "40015", DataType = DataValueType.Float, Unit = "dB" }
            });

            // 产线设备数据点
            var deviceProd = devices.First(d => d.Code == "SIM_PROD_LINE");
            dataPoints.AddRange(new[]
            {
                new DataPoint { DeviceId = deviceProd.Id, Name = "主轴温度", Tag = "SpindleTemp", Description = "主轴温度", Address = "40001", DataType = DataValueType.Float, Unit = "℃" },
                new DataPoint { DeviceId = deviceProd.Id, Name = "主轴转速", Tag = "SpindleSpeed", Description = "主轴转速", Address = "40003", DataType = DataValueType.Float, Unit = "rpm" },
                new DataPoint { DeviceId = deviceProd.Id, Name = "进给速度", Tag = "FeedRate", Description = "进给速度", Address = "40005", DataType = DataValueType.Float, Unit = "mm/min" },
                new DataPoint { DeviceId = deviceProd.Id, Name = "切削力", Tag = "CuttingForce", Description = "切削力", Address = "40007", DataType = DataValueType.Float, Unit = "N" },
                new DataPoint { DeviceId = deviceProd.Id, Name = "振动", Tag = "Vibration", Description = "振动值", Address = "40009", DataType = DataValueType.Float, Unit = "mm/s" },
                new DataPoint { DeviceId = deviceProd.Id, Name = "功率", Tag = "Power", Description = "主轴功率", Address = "40011", DataType = DataValueType.Float, Unit = "kW" }
            });

            // 动力站设备数据点
            var devicePower = devices.First(d => d.Code == "SIM_POWER");
            dataPoints.AddRange(new[]
            {
                new DataPoint { DeviceId = devicePower.Id, Name = "电压 A", Tag = "VoltageA", Description = "A 相电压", Address = "40001", DataType = DataValueType.Float, Unit = "V" },
                new DataPoint { DeviceId = devicePower.Id, Name = "电压 B", Tag = "VoltageB", Description = "B 相电压", Address = "40003", DataType = DataValueType.Float, Unit = "V" },
                new DataPoint { DeviceId = devicePower.Id, Name = "电压 C", Tag = "VoltageC", Description = "C 相电压", Address = "40005", DataType = DataValueType.Float, Unit = "V" },
                new DataPoint { DeviceId = devicePower.Id, Name = "电流 A", Tag = "CurrentA", Description = "A 相电流", Address = "40007", DataType = DataValueType.Float, Unit = "A" },
                new DataPoint { DeviceId = devicePower.Id, Name = "电流 B", Tag = "CurrentB", Description = "B 相电流", Address = "40009", DataType = DataValueType.Float, Unit = "A" },
                new DataPoint { DeviceId = devicePower.Id, Name = "电流 C", Tag = "CurrentC", Description = "C 相电流", Address = "40011", DataType = DataValueType.Float, Unit = "A" },
                new DataPoint { DeviceId = devicePower.Id, Name = "功率因数", Tag = "PowerFactor", Description = "功率因数", Address = "40013", DataType = DataValueType.Float, Unit = "" },
                new DataPoint { DeviceId = devicePower.Id, Name = "频率", Tag = "Frequency", Description = "电网频率", Address = "40015", DataType = DataValueType.Float, Unit = "Hz" },
                new DataPoint { DeviceId = devicePower.Id, Name = "累计电量", Tag = "TotalEnergy", Description = "累计电量", Address = "40017", DataType = DataValueType.Float, Unit = "kWh" }
            });

            await context.DataPoints.AddRangeAsync(dataPoints);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个测试数据点", dataPoints.Count);

            // 3. 插入测试虚拟数据点
            var virtualPoints = new List<VirtualDataPoint>
            {
                // 设备 1 的虚拟点
                new() { DeviceId = device1.Id, Name = "平均温度", Tag = "SIM_DEVICE_001.Virtual.AvgTemp", Description = "温度平均值", Expression = "Avg(SIM_DEVICE_001.Temperature01, SIM_DEVICE_001.Temperature02, SIM_DEVICE_001.Temperature03)", CalculationType = CalculationType.Average, DataType = DataValueType.Float, Unit = "℃", DependencyTags = "[\"SIM_DEVICE_001.Temperature01\",\"SIM_DEVICE_001.Temperature02\",\"SIM_DEVICE_001.Temperature03\"]" },
                new() { DeviceId = device1.Id, Name = "温压积", Tag = "SIM_DEVICE_001.Virtual.TempPressProd", Description = "温度压力乘积", Expression = "SIM_DEVICE_001.Temperature01 * SIM_DEVICE_001.Pressure01", CalculationType = CalculationType.Custom, DataType = DataValueType.Float, Unit = "", DependencyTags = "[\"SIM_DEVICE_001.Temperature01\",\"SIM_DEVICE_001.Pressure01\"]" },
                new() { DeviceId = device1.Id, Name = "温差", Tag = "SIM_DEVICE_001.Virtual.TempDiff", Description = "温度差值", Expression = "SIM_DEVICE_001.Temperature01 - SIM_DEVICE_001.Temperature02", CalculationType = CalculationType.Custom, DataType = DataValueType.Float, Unit = "℃", DependencyTags = "[\"SIM_DEVICE_001.Temperature01\",\"SIM_DEVICE_001.Temperature02\"]" },
                // 设备 2 的虚拟点
                new() { DeviceId = device2.Id, Name = "流量差", Tag = "SIM_DEVICE_002.Virtual.FlowDiff", Description = "进出口流量差", Expression = "SIM_DEVICE_002.Flow01 - SIM_DEVICE_002.Flow02", CalculationType = CalculationType.Custom, DataType = DataValueType.Float, Unit = "m³/h", DependencyTags = "[\"SIM_DEVICE_002.Flow01\",\"SIM_DEVICE_002.Flow02\"]" },
                new() { DeviceId = device2.Id, Name = "平均液位", Tag = "SIM_DEVICE_002.Virtual.AvgLevel", Description = "平均液位", Expression = "Avg(SIM_DEVICE_002.Level01, SIM_DEVICE_002.Level02)", CalculationType = CalculationType.Average, DataType = DataValueType.Float, Unit = "m", DependencyTags = "[\"SIM_DEVICE_002.Level01\",\"SIM_DEVICE_002.Level02\"]" },
                // 设备 3 的虚拟点
                new() { DeviceId = device3.Id, Name = "空气质量指数", Tag = "SIM_DEVICE_003.Virtual.AQI", Description = "简易空气质量指数", Expression = "Avg(SIM_DEVICE_003.PM25, SIM_DEVICE_003.PM10)", CalculationType = CalculationType.Average, DataType = DataValueType.Float, Unit = "μg/m³", DependencyTags = "[\"SIM_DEVICE_003.PM25\",\"SIM_DEVICE_003.PM10\"]" },
                new() { DeviceId = device3.Id, Name = "舒适度指数", Tag = "SIM_DEVICE_003.Virtual.ComfortIndex", Description = "温湿度舒适度", Expression = "SIM_DEVICE_003.Temperature * 0.6 + SIM_DEVICE_003.Humidity * 0.4", CalculationType = CalculationType.Custom, DataType = DataValueType.Float, Unit = "", DependencyTags = "[\"SIM_DEVICE_003.Temperature\",\"SIM_DEVICE_003.Humidity\"]" },
                // 产线设备虚拟点
                new() { DeviceId = deviceProd.Id, Name = "平均温度", Tag = "SIM_PROD_LINE.Virtual.AvgTemp", Description = "平均工作温度", Expression = "Avg(SIM_PROD_LINE.SpindleTemp)", CalculationType = CalculationType.Average, DataType = DataValueType.Float, Unit = "℃", DependencyTags = "[\"SIM_PROD_LINE.SpindleTemp\"]" },
                new() { DeviceId = deviceProd.Id, Name = "能效比", Tag = "SIM_PROD_LINE.Virtual.Efficiency", Description = "能效比", Expression = "SIM_PROD_LINE.Power / SIM_PROD_LINE.SpindleSpeed", CalculationType = CalculationType.Custom, DataType = DataValueType.Float, Unit = "kW/rpm", DependencyTags = "[\"SIM_PROD_LINE.Power\",\"SIM_PROD_LINE.SpindleSpeed\"]" },
                // 动力站虚拟点
                new() { DeviceId = devicePower.Id, Name = "三相平均电压", Tag = "SIM_POWER.Virtual.AvgVoltage", Description = "三相平均电压", Expression = "Avg(SIM_POWER.VoltageA, SIM_POWER.VoltageB, SIM_POWER.VoltageC)", CalculationType = CalculationType.Average, DataType = DataValueType.Float, Unit = "V", DependencyTags = "[\"SIM_POWER.VoltageA\",\"SIM_POWER.VoltageB\",\"SIM_POWER.VoltageC\"]" },
                new() { DeviceId = devicePower.Id, Name = "三相平均电流", Tag = "SIM_POWER.Virtual.AvgCurrent", Description = "三相平均电流", Expression = "Avg(SIM_POWER.CurrentA, SIM_POWER.CurrentB, SIM_POWER.CurrentC)", CalculationType = CalculationType.Average, DataType = DataValueType.Float, Unit = "A", DependencyTags = "[\"SIM_POWER.CurrentA\",\"SIM_POWER.CurrentB\",\"SIM_POWER.CurrentC\"]" },
                new() { DeviceId = devicePower.Id, Name = "总功率", Tag = "SIM_POWER.Virtual.TotalPower", Description = "估算总功率", Expression = "SIM_POWER.VoltageA * SIM_POWER.CurrentA + SIM_POWER.VoltageB * SIM_POWER.CurrentB + SIM_POWER.VoltageC * SIM_POWER.CurrentC", CalculationType = CalculationType.Custom, DataType = DataValueType.Float, Unit = "kW", DependencyTags = "[\"SIM_POWER.VoltageA\",\"SIM_POWER.CurrentA\",\"SIM_POWER.VoltageB\",\"SIM_POWER.CurrentB\",\"SIM_POWER.VoltageC\",\"SIM_POWER.CurrentC\"]" }
            };

            await context.VirtualDataPoints.AddRangeAsync(virtualPoints);
            await context.SaveChangesAsync();
            _logger.LogInformation("已插入 {Count} 个测试虚拟数据点", virtualPoints.Count);

            // 4. 插入测试发送通道（仅 HTTP 服务端和 WebSocket）
            var channels = new List<Channel>
            {
                new() { Name = "HTTP 数据接口", Code = "HTTP_SERVER", Description = "提供 HTTP 数据接口", Protocol = SendProtocol.Http, Endpoint = "/api/v1/telemetry", IsEnabled = true, HttpMethod = "POST", HttpTimeout = 5000, HttpMode = "server" },
                new() { Name = "WebSocket 推送", Code = "WS_PUSH", Description = "WebSocket 实时推送", Protocol = SendProtocol.WebSocket, Endpoint = "/ws", IsEnabled = true, WsSubscribeTopic = "edge/telemetry", WsHeartbeatInterval = 30000 }
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

            _logger.LogInformation("测试数据初始化完成！设备：{Devices}, 数据点：{DataPoints}, 虚拟点：{VirtualPoints}, 通道：{Channels}, 映射：{Mappings}",
                devices.Count, dataPoints.Count, virtualPoints.Count, channels.Count, mappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试数据初始化失败");
            throw;
        }
    }
}
