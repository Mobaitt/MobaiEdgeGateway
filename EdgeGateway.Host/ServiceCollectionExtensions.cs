using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using EdgeGateway.Infrastructure.Repositories;
using EdgeGateway.Infrastructure.Strategies.Collection;
using EdgeGateway.Infrastructure.Strategies.Send;
using EdgeGateway.Infrastructure.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EdgeGateway.Host;

/// <summary>
/// 依赖注入注册扩展类
/// 将所有层的服务统一在此注册，保持 Program.cs 简洁
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册边缘网关所有服务
    /// </summary>
    public static IServiceCollection AddEdgeGateway(
        this IServiceCollection services,
        string dbPath = "gateway.db")
    {
        // ========== 数据库 ==========
        services.AddDbContext<GatewayDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}")
                   .EnableSensitiveDataLogging(false));

        // ========== 仓储层 ==========
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDataPointRepository, DataPointRepository>();
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IChannelMappingRepository, ChannelMappingRepository>();

        // ========== 采集策略实现（Transient：每次获取新实例，避免跨设备状态污染）==========
        services.AddTransient<SimulatorCollectionStrategy>();
        services.AddTransient<ModbusCollectionStrategy>();
        // 其他策略（OpcUa、S7 等）按需在此注册

        // ========== 发送策略实现 ==========
        services.AddTransient<LocalFileSendStrategy>();
        services.AddTransient<MqttSendStrategy>();
        services.AddTransient<HttpSendStrategy>();
        services.AddTransient<WebSocketSendStrategy>();
        // 其他策略（Kafka 等）按需在此注册

        // ========== WebSocket 连接管理器（单例） ==========
        services.AddSingleton<WebSocketConnectionManager>();

        // ========== 策略注册器（工厂） ==========
        // 采集策略注册器：协议枚举 → 策略类型
        services.AddSingleton<CollectionStrategyRegistry>(sp =>
        {
            var registry = new CollectionStrategyRegistry(sp,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CollectionStrategyRegistry>>());

            // 在此注册所有支持的采集协议
            registry.Register<SimulatorCollectionStrategy>(CollectionProtocol.Simulator);
            registry.Register<ModbusCollectionStrategy>(CollectionProtocol.Modbus);
            // registry.Register<OpcUaCollectionStrategy>(CollectionProtocol.OpcUa);
            // registry.Register<S7CollectionStrategy>(CollectionProtocol.S7);

            return registry;
        });

        // 发送策略注册器：协议枚举 → 策略类型
        services.AddSingleton<SendStrategyRegistry>(sp =>
        {
            var registry = new SendStrategyRegistry(sp,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SendStrategyRegistry>>());

            // 在此注册所有支持的发送协议
            registry.Register<LocalFileSendStrategy>(SendProtocol.LocalFile);
            registry.Register<MqttSendStrategy>(SendProtocol.Mqtt);
            registry.Register<HttpSendStrategy>(SendProtocol.Http);
            registry.Register<WebSocketSendStrategy>(SendProtocol.WebSocket);
            // registry.Register<KafkaSendStrategy>(SendProtocol.Kafka);

            return registry;
        });

        // 注册发送策略注册器接口
        services.AddSingleton<ISendStrategyRegistry>(sp => sp.GetRequiredService<SendStrategyRegistry>());

        // ========== 应用层服务 ==========
        services.AddScoped<DeviceManagementService>();
        services.AddSingleton<DataSendService>();
        services.AddSingleton<DataCollectionService>();
        services.AddSingleton<RealtimeDataService>();

        // HTTP 客户端（供 HttpSendStrategy 使用）
        services.AddHttpClient("GatewayHttpClient");

        return services;
    }

    /// <summary>
    /// 确保数据库已创建并应用迁移（开发环境使用 EnsureCreated，生产建议用 Migrate）
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
