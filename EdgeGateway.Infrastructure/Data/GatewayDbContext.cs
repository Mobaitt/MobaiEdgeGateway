using EdgeGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EdgeGateway.Infrastructure.Data;

/// <summary>
/// 边缘网关数据库上下文（使用SQLite存储设备配置信息）
/// </summary>
public class GatewayDbContext : DbContext
{
    public GatewayDbContext(DbContextOptions<GatewayDbContext> options) : base(options) { }

    /// <summary>设备表</summary>
    public DbSet<Device> Devices => Set<Device>();

    /// <summary>数据点表</summary>
    public DbSet<DataPoint> DataPoints => Set<DataPoint>();

    /// <summary>发送通道表</summary>
    public DbSet<Channel> Channels => Set<Channel>();

    /// <summary>通道与数据点映射关系表</summary>
    public DbSet<ChannelDataPointMapping> ChannelDataPointMappings => Set<ChannelDataPointMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ============ 设备表配置 ============
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();

            // 一个设备有多个数据点
            entity.HasMany(e => e.DataPoints)
                  .WithOne(dp => dp.Device)
                  .HasForeignKey(dp => dp.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============ 数据点表配置 ============
        modelBuilder.Entity<DataPoint>(entity =>
        {
            entity.ToTable("DataPoints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Tag).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Tag).IsUnique(); // Tag全局唯一

            // 数据点与通道映射：一个数据点对应多条映射记录
            entity.HasMany(e => e.ChannelMappings)
                  .WithOne(m => m.DataPoint)
                  .HasForeignKey(m => m.DataPointId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============ 通道表配置 ============
        modelBuilder.Entity<Channel>(entity =>
        {
            entity.ToTable("Channels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Code).IsUnique();

            // 通道与数据点映射：一个通道对应多条映射记录
            entity.HasMany(e => e.DataPointMappings)
                  .WithOne(m => m.Channel)
                  .HasForeignKey(m => m.ChannelId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============ 映射关系表配置 ============
        modelBuilder.Entity<ChannelDataPointMapping>(entity =>
        {
            entity.ToTable("ChannelDataPointMappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AliasName).HasMaxLength(100);

            // 同一通道不能重复添加同一数据点
            entity.HasIndex(e => new { e.ChannelId, e.DataPointId }).IsUnique();
        });

        // ============ 种子数据（测试用）============
        SeedTestData(modelBuilder);
    }

    /// <summary>
    /// 写入测试种子数据，方便快速验证功能
    /// </summary>
    private static void SeedTestData(ModelBuilder modelBuilder)
    {
        // 添加一个模拟设备（用于演示，不需要真实硬件）
        modelBuilder.Entity<Device>().HasData(new Device
        {
            Id = 1,
            Name = "模拟温度设备",
            Code = "DEV_SIMULATOR_001",
            Description = "用于本地测试的模拟数据设备",
            Protocol = Domain.Enums.CollectionProtocol.Simulator,
            Address = "localhost",
            PollingIntervalMs = 2000,
            IsEnabled = true,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // 添加数据点
        modelBuilder.Entity<DataPoint>().HasData(
            new DataPoint
            {
                Id = 1, DeviceId = 1,
                Name = "温度", Tag = "DEV_SIMULATOR_001.Temperature",
                Address = "40001", DataType = Domain.Enums.DataValueType.Float,
                Unit = "℃", IsEnabled = true, RegisterLength = 2,
                ModbusSlaveId = 1, ModbusFunctionCode = 3, ModbusByteOrder = Domain.Enums.ModbusByteOrder.ABCD,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new DataPoint
            {
                Id = 2, DeviceId = 1,
                Name = "压力", Tag = "DEV_SIMULATOR_001.Pressure",
                Address = "40002", DataType = Domain.Enums.DataValueType.Float,
                Unit = "MPa", IsEnabled = true, RegisterLength = 2,
                ModbusSlaveId = 1, ModbusFunctionCode = 3, ModbusByteOrder = Domain.Enums.ModbusByteOrder.ABCD,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // 添加一个本地文件发送通道
        modelBuilder.Entity<Channel>().HasData(new Channel
        {
            Id = 1,
            Name = "本地文件通道",
            Code = "CH_LOCAL_FILE",
            Description = "将采集数据写入本地JSON文件",
            Protocol = Domain.Enums.SendProtocol.LocalFile,
            Endpoint = "./output/data.json",
            IsEnabled = true,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // 建立映射：温度和压力数据点 → 本地文件通道
        modelBuilder.Entity<ChannelDataPointMapping>().HasData(
            new ChannelDataPointMapping { Id = 1, ChannelId = 1, DataPointId = 1, IsEnabled = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ChannelDataPointMapping { Id = 2, ChannelId = 1, DataPointId = 2, IsEnabled = true, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
