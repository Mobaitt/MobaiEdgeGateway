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

    /// <summary>数据点规则表</summary>
    public DbSet<DataPointRule> DataPointRules => Set<DataPointRule>();

    /// <summary>虚拟数据点表</summary>
    public DbSet<VirtualDataPoint> VirtualDataPoints => Set<VirtualDataPoint>();

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

            // Tag 全局唯一
            entity.HasIndex(e => e.Tag).IsUnique();

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

            // 通道与虚拟数据点映射 - 使用相同的导航属性，但配置不同的外键集合
            entity.HasMany(e => e.VirtualDataPointMappings)
                  .WithOne()
                  .HasForeignKey(m => m.ChannelId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============ 映射关系表配置 ============
        modelBuilder.Entity<ChannelDataPointMapping>(entity =>
        {
            entity.ToTable("ChannelDataPointMappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DataPointTag).HasMaxLength(200);
            entity.Property(e => e.DataPointName).HasMaxLength(100);

            // DataPointId 和 VirtualDataPointId 至少有一个有值
            entity.HasCheckConstraint("CK_ChannelDataPointMapping_DataPoint", "DataPointId IS NOT NULL OR VirtualDataPointId IS NOT NULL");

            // 同一通道不能重复添加同一数据点（包括普通数据点和虚拟数据点）
            entity.HasIndex(e => new { e.ChannelId, e.DataPointId }).IsUnique();
            entity.HasIndex(e => new { e.ChannelId, e.VirtualDataPointId }).IsUnique();
        });

        // ============ 数据点规则表配置 ============
        modelBuilder.Entity<DataPointRule>(entity =>
        {
            entity.ToTable("DataPointRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RuleConfig).IsRequired();

            // 外键关系
            entity.HasOne(r => r.DataPoint)
                  .WithMany()
                  .HasForeignKey(r => r.DataPointId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Device)
                  .WithMany()
                  .HasForeignKey(r => r.DeviceId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============ 虚拟数据点表配置 ============
        modelBuilder.Entity<VirtualDataPoint>(entity =>
        {
            entity.ToTable("VirtualDataPoints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Tag).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Expression).IsRequired();

            // Tag 全局唯一
            entity.HasIndex(e => e.Tag).IsUnique();

            // 虚拟数据点属于某个设备
            entity.HasOne(vp => vp.Device)
                  .WithMany()
                  .HasForeignKey(vp => vp.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);

            // 虚拟数据点与通道映射
            entity.HasMany(e => e.ChannelMappings)
                  .WithOne(m => m.VirtualDataPoint)
                  .HasForeignKey(m => m.VirtualDataPointId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============ 种子数据（测试用）============
        // SeedTestData(modelBuilder);
    }
}
