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
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace EdgeGateway.Host;

/// <summary>
/// 依赖注入注册扩展类。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册边缘网关所需服务。
    /// </summary>
    public static IServiceCollection AddEdgeGateway(
        this IServiceCollection services,
        string dbPath = "gateway.db")
    {
        services.Configure<GatewayOptions>(options =>
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
            options.Database.EnableSensitiveDataLogging = false;
        });

        services.AddDbContext<GatewayDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}")
                   .EnableSensitiveDataLogging()
                   .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information));

        services.AddDbContextFactory<GatewayDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}")
                   .EnableSensitiveDataLogging()
                   .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information));

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDataPointRepository, DataPointRepository>();
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IChannelMappingRepository, ChannelMappingRepository>();

        services.AddTransient<SimulatorCollectionStrategy>();
        services.AddTransient<ModbusCollectionStrategy>();

        services.AddTransient<LocalFileSendStrategy>();
        services.AddTransient<MqttSendStrategy>();
        services.AddTransient<HttpSendStrategy>();
        services.AddTransient<WebSocketSendStrategy>();

        services.AddSingleton<WebSocketConnectionManager>();

        services.AddSingleton<HttpListenerService>();
        services.AddSingleton<IHttpListenerService>(sp => sp.GetRequiredService<HttpListenerService>());

        services.AddSingleton<CollectionStrategyRegistry>(sp =>
        {
            var registry = new CollectionStrategyRegistry(sp,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CollectionStrategyRegistry>>());

            registry.Register<SimulatorCollectionStrategy>(CollectionProtocol.Simulator);
            registry.Register<ModbusCollectionStrategy>(CollectionProtocol.Modbus);

            return registry;
        });

        services.AddSingleton<SendStrategyRegistry>(sp =>
        {
            var registry = new SendStrategyRegistry(sp,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SendStrategyRegistry>>());

            registry.Register<LocalFileSendStrategy>(SendProtocol.LocalFile);
            registry.Register<MqttSendStrategy>(SendProtocol.Mqtt);
            registry.Register<HttpSendStrategy>(SendProtocol.Http);
            registry.Register<WebSocketSendStrategy>(SendProtocol.WebSocket);

            return registry;
        });

        services.AddSingleton<ISendStrategyRegistry>(sp => sp.GetRequiredService<SendStrategyRegistry>());

        services.AddScoped<DeviceManagementService>();
        services.AddScoped<DataPointControlService>();
        services.AddSingleton<DataSendService>();
        services.AddSingleton<DataCollectionService>();
        services.AddScoped<RuleManagementService>();
        services.AddScoped<VirtualNodeManagementService>();

        services.AddSingleton<IRuleEngine, EdgeGateway.Infrastructure.Rules.RuleEngine>();
        services.AddSingleton<IVirtualNodeEngine, EdgeGateway.Infrastructure.VirtualNodes.VirtualNodeEngine>();

        services.AddSingleton<DatabaseSeeder>();

        services.AddHttpClient("GatewayHttpClient");

        return services;
    }

    /// <summary>
    /// 确保数据库已创建，并在启动时按实体定义补齐缺失字段。
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

        await db.Database.EnsureCreatedAsync();

        // 保留历史特殊迁移，处理旧字段升级为新结构。
        await MigrateDataPointIdColumnAsync(db);

        // 根据实体定义自动补齐缺失列。
        await SyncEntityColumnsAsync(db);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.InitializeTestDataAsync();
    }

    /// <summary>
    /// 迁移旧版 DataPointRules.DataPointId 到 DataPointIdsJson。
    /// </summary>
    private static async Task MigrateDataPointIdColumnAsync(GatewayDbContext db)
    {
        try
        {
            var tableInfo = await GetTableInfoAsync(db, "DataPointRules");
            var hasDataPointIdColumn = tableInfo.Any(c => c.Name == "DataPointId");
            var hasDataPointIdsJsonColumn = tableInfo.Any(c => c.Name == "DataPointIdsJson");

            if (!hasDataPointIdColumn || hasDataPointIdsJsonColumn)
                return;

            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"DataPointRules\" ADD COLUMN \"DataPointIdsJson\" TEXT");

            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "DataPointRules"
                SET "DataPointIdsJson" = '[' || "DataPointId" || ']'
                WHERE "DataPointId" IS NOT NULL
                """);

            Console.WriteLine("数据库迁移完成：DataPointId -> DataPointIdsJson");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"数据库迁移失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 根据实体定义补齐缺失列。
    /// 仅处理新增列，不处理删列、改名和类型变更。
    /// </summary>
    private static async Task SyncEntityColumnsAsync(GatewayDbContext db)
    {
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.FindPrimaryKey() == null)
                continue;

            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
                continue;

            var schema = entityType.GetSchema();
            var storeObject = StoreObjectIdentifier.Table(tableName, schema);
            var existingColumns = await GetTableInfoAsync(db, tableName);
            var existingColumnNames = existingColumns
                .Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var property in entityType.GetProperties())
            {
                if (property.IsPrimaryKey())
                    continue;

                var columnName = property.GetColumnName(storeObject);
                if (string.IsNullOrWhiteSpace(columnName) || existingColumnNames.Contains(columnName))
                    continue;

                var sql = BuildAddColumnSql(tableName, columnName, property);
                await db.Database.ExecuteSqlRawAsync(sql);

                Console.WriteLine($"数据库字段已补齐：{tableName}.{columnName}");
            }
        }
    }

    private static async Task<List<ColumnInfo>> GetTableInfoAsync(GatewayDbContext db, string tableName)
    {
        var result = new List<ColumnInfo>();
        var escapedTableName = EscapeSqliteIdentifier(tableName);

        await using var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{escapedTableName}\")";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ColumnInfo
            {
                Cid = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Notnull = !reader.IsDBNull(3) && reader.GetInt32(3) == 1,
                DfltValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                Pk = !reader.IsDBNull(5) ? reader.GetInt32(5) : 0
            });
        }

        return result;
    }

    private static string BuildAddColumnSql(string tableName, string columnName, IProperty property)
    {
        var escapedTableName = EscapeSqliteIdentifier(tableName);
        var escapedColumnName = EscapeSqliteIdentifier(columnName);
        var storeType = property.GetColumnType() ?? property.GetRelationalTypeMapping().StoreType;
        var nullability = property.IsNullable ? "NULL" : "NOT NULL";
        var defaultClause = BuildDefaultClause(property);

        return $"ALTER TABLE \"{escapedTableName}\" ADD COLUMN \"{escapedColumnName}\" {storeType} {nullability}{defaultClause}";
    }

    private static string BuildDefaultClause(IProperty property)
    {
        if (property.IsNullable)
            return string.Empty;

        var defaultValueSql = property.GetDefaultValueSql();
        if (!string.IsNullOrWhiteSpace(defaultValueSql))
            return $" DEFAULT ({defaultValueSql})";

        var defaultValue = property.GetDefaultValue();
        if (defaultValue is null)
            defaultValue = GetClrDefaultValue(property.ClrType);

        return defaultValue is null ? string.Empty : $" DEFAULT {FormatSqlLiteral(defaultValue, property.ClrType)}";
    }

    private static object? GetClrDefaultValue(Type clrType)
    {
        var actualType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (actualType == typeof(string))
            return string.Empty;

        if (actualType == typeof(Guid))
            return Guid.Empty;

        if (actualType == typeof(DateTime))
            return DateTime.MinValue;

        if (actualType == typeof(DateTimeOffset))
            return DateTimeOffset.MinValue;

        if (actualType == typeof(byte[]))
            return Array.Empty<byte>();

        return actualType.IsValueType ? Activator.CreateInstance(actualType) : null;
    }

    private static string FormatSqlLiteral(object value, Type clrType)
    {
        var actualType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (actualType.IsEnum)
        {
            var numericValue = Convert.ChangeType(value, Enum.GetUnderlyingType(actualType));
            return Convert.ToString(numericValue, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        }

        if (actualType == typeof(bool))
            return (bool)value ? "1" : "0";

        if (actualType == typeof(string))
            return $"'{EscapeSqlLiteral((string)value)}'";

        if (actualType == typeof(Guid))
            return $"'{value}'";

        if (actualType == typeof(DateTime))
            return $"'{((DateTime)value).ToString("O", System.Globalization.CultureInfo.InvariantCulture)}'";

        if (actualType == typeof(DateTimeOffset))
            return $"'{((DateTimeOffset)value).ToString("O", System.Globalization.CultureInfo.InvariantCulture)}'";

        if (actualType == typeof(byte[]))
            return "X''";

        if (actualType == typeof(float) ||
            actualType == typeof(double) ||
            actualType == typeof(decimal))
        {
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        }

        if (actualType == typeof(byte) ||
            actualType == typeof(sbyte) ||
            actualType == typeof(short) ||
            actualType == typeof(ushort) ||
            actualType == typeof(int) ||
            actualType == typeof(uint) ||
            actualType == typeof(long) ||
            actualType == typeof(ulong))
        {
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        }

        return $"'{EscapeSqlLiteral(value.ToString() ?? string.Empty)}'";
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''");
    }

    private static string EscapeSqliteIdentifier(string identifier)
    {
        return identifier.Replace("\"", "\"\"");
    }

    private sealed class ColumnInfo
    {
        public int Cid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Notnull { get; set; }
        public string? DfltValue { get; set; }
        public int Pk { get; set; }
    }
}
