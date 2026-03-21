using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Domain.Options;
using EdgeGateway.Infrastructure.Data;
using EdgeGateway.Infrastructure.Http;
using EdgeGateway.Infrastructure.Repositories;
using EdgeGateway.Infrastructure.Strategies.Collection;
using EdgeGateway.Infrastructure.Strategies.Send;
using EdgeGateway.Infrastructure.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        IConfiguration? configuration = null,
        string dbPath = "gateway.db")
    {
        var optionsBuilder = services.AddOptions<GatewayOptions>()
            .Configure(options => ApplyDefaultGatewayOptions(options, dbPath))
            .ValidateOnStart();

        if (configuration != null)
        {
            optionsBuilder.Bind(configuration.GetSection("GatewayOptions"));
        }

        services.AddSingleton<IValidateOptions<GatewayOptions>, GatewayOptionsValidator>();

        // ========== 数据库 ==========
        services.AddDbContext<GatewayDbContext>((serviceProvider, options) =>
        {
            var gatewayOptions = serviceProvider.GetRequiredService<IOptions<GatewayOptions>>().Value;
            options.UseSqlite(gatewayOptions.Database.ConnectionString);

            if (gatewayOptions.Database.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging()
                       .LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        // 注册 IDbContextFactory（供 RuleEngine 等使用）
        services.AddDbContextFactory<GatewayDbContext>((serviceProvider, options) =>
        {
            var gatewayOptions = serviceProvider.GetRequiredService<IOptions<GatewayOptions>>().Value;
            options.UseSqlite(gatewayOptions.Database.ConnectionString);

            if (gatewayOptions.Database.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging()
                       .LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

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

        // ========== HTTP 监听服务（单例，供 HTTP 服务端模式使用） ==========
        services.AddSingleton<HttpListenerService>();
        services.AddSingleton<IHttpListenerService>(sp => sp.GetRequiredService<HttpListenerService>());

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
        services.AddScoped<RuleManagementService>();
        services.AddScoped<VirtualNodeManagementService>();

        // ========== 规则引擎和虚拟节点引擎 ==========
        services.AddSingleton<IRuleEngine, EdgeGateway.Infrastructure.Rules.RuleEngine>();
        services.AddSingleton<IVirtualNodeEngine, EdgeGateway.Infrastructure.VirtualNodes.VirtualNodeEngine>();

        // 数据库种子服务
        services.AddSingleton<DatabaseSeeder>();

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
        var options = scope.ServiceProvider.GetRequiredService<IOptions<GatewayOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        await InitializeDatabaseSchemaAsync(db, options, logger);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        if (string.Equals(options.Database.SeedMode, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            await seeder.InitializeTestDataAsync();
        }
        else
        {
            logger.LogInformation("已跳过演示种子数据初始化，当前 SeedMode={SeedMode}", options.Database.SeedMode);
        }
    }

    private static void ApplyDefaultGatewayOptions(GatewayOptions options, string dbPath)
    {
        options.Collection.AggregateWindowMs = 1000;
        options.Collection.DataExpirationSeconds = 30;
        options.Collection.DefaultPollingIntervalMs = 1000;
        options.Collection.MinPollingIntervalMs = 100;
        options.Collection.MaxPollingIntervalMs = 60000;

        options.Send.ChannelCacheExpirationSeconds = 30;
        options.Send.HttpTimeoutMs = 5000;
        options.Send.MqttQoS = 1;
        options.Send.MaxConcurrentChannels = 10;

        options.Rules.CacheExpirationMinutes = 5;

        options.VirtualNodes.CalculationCacheMs = 500;
        options.VirtualNodes.MaxConcurrentCalculations = 20;

        options.Database.Type = "SQLite";
        options.Database.ConnectionString = $"Data Source={dbPath}";
        options.Database.InitializationMode = "Auto";
        options.Database.SeedMode = "Demo";
        options.Database.EnableSensitiveDataLogging = false;
    }

    private static async Task InitializeDatabaseSchemaAsync(
        GatewayDbContext db,
        GatewayOptions options,
        ILogger logger)
    {
        var mode = options.Database.InitializationMode.Trim();

        if (string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            var hasMigrations = (await db.Database.GetMigrationsAsync()).Any();

            if (hasMigrations)
            {
                logger.LogInformation("检测到 EF Core 迁移，按 Auto 模式执行 Migrate");
                await db.Database.MigrateAsync();
            }
            else
            {
                logger.LogInformation("未检测到 EF Core 迁移，按 Auto 模式执行 EnsureCreated");
                await db.Database.EnsureCreatedAsync();
            }

            return;
        }

        if (string.Equals(mode, "Migrate", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("按配置执行数据库迁移初始化");
            await db.Database.MigrateAsync();
            return;
        }

        logger.LogInformation("按配置执行 EnsureCreated 数据库初始化");
        await db.Database.EnsureCreatedAsync();
    }
}
