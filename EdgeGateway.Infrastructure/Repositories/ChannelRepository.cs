using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EdgeGateway.Infrastructure.Repositories;

/// <summary>
/// 发送通道仓储实现（EF Core + SQLite）
/// </summary>
public class ChannelRepository : IChannelRepository
{
    private readonly GatewayDbContext _db;

    public ChannelRepository(GatewayDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<Channel?> GetByIdAsync(int id) =>
        await _db.Channels
            .Include(c => c.DataPointMappings)
            .Include(c => c.VirtualDataPointMappings)
            .FirstOrDefaultAsync(c => c.Id == id);

    /// <inheritdoc/>
    /// <summary>无跟踪查询，用于获取原始值进行比较</summary>
    public async Task<Channel?> GetByIdNoTrackingAsync(int id) =>
        await _db.Channels
            .AsNoTracking()
            .Include(c => c.DataPointMappings)
            .Include(c => c.VirtualDataPointMappings)
            .FirstOrDefaultAsync(c => c.Id == id);

    /// <inheritdoc/>
    public async Task<IEnumerable<Channel>> GetAllAsync()
    {
        var channels = await _db.Channels.ToListAsync();
        
        // 手动加载两种映射
        foreach (var channel in channels)
        {
            channel.DataPointMappings = await _db.ChannelDataPointMappings
                .Where(m => m.ChannelId == channel.Id && m.DataPointId.HasValue)
                .Include(m => m.DataPoint)
                .ToListAsync();
            
            channel.VirtualDataPointMappings = await _db.ChannelDataPointMappings
                .Where(m => m.ChannelId == channel.Id && m.VirtualDataPointId.HasValue)
                .Include(m => m.VirtualDataPoint)
                .ToListAsync();
        }
        
        return channels;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Channel>> GetEnabledAsync() =>
        await _db.Channels
            .Where(c => c.IsEnabled)
            .ToListAsync();

    /// <inheritdoc/>
    /// <remarks>
    /// 加载通道时同时 Include 映射关系及数据点详情
    /// 供发送服务（DataSendService）使用，避免多次查询
    /// </remarks>
    public async Task<IEnumerable<Channel>> GetEnabledWithMappingsAsync()
    {
        var channels = await _db.Channels
            .Where(c => c.IsEnabled)
            .ToListAsync();
        
        // 手动加载两种映射
        foreach (var channel in channels)
        {
            // 加载普通数据点映射
            channel.DataPointMappings = await _db.ChannelDataPointMappings
                .Where(m => m.ChannelId == channel.Id && m.IsEnabled && m.DataPointId.HasValue)
                .Include(m => m.DataPoint)
                .ToListAsync();
            
            // 加载虚拟数据点映射
            channel.VirtualDataPointMappings = await _db.ChannelDataPointMappings
                .Where(m => m.ChannelId == channel.Id && m.IsEnabled && m.VirtualDataPointId.HasValue)
                .Include(m => m.VirtualDataPoint)
                .ToListAsync();
            
            // 详细调试日志
            // var allMappings = await _db.ChannelDataPointMappings
            //     .Where(m => m.ChannelId == channel.Id)
            //     .Select(m => new { m.Id, m.DataPointId, m.VirtualDataPointId, m.IsEnabled })
            //     .ToListAsync();
            
            // foreach (var m in allMappings)
            // {
            //     Console.WriteLine($"通道 {channel.Name} 映射：Id={m.Id}, DataPointId={m.DataPointId}, VirtualDataPointId={m.VirtualDataPointId}, IsEnabled={m.IsEnabled}");
            // }
            //
            // Console.WriteLine($"通道 {channel.Name}: DataPointMappings={channel.DataPointMappings.Count}, VirtualDataPointMappings={channel.VirtualDataPointMappings.Count}");
        }
        
        return channels;
    }

    /// <inheritdoc/>
    public async Task<Channel> AddAsync(Channel channel)
    {
        channel.CreatedAt = DateTime.UtcNow;
        _db.Channels.Add(channel);
        await _db.SaveChangesAsync();
        return channel;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Channel channel)
    {
        _db.Channels.Update(channel);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        var channel = await _db.Channels.FindAsync(id);
        if (channel != null)
        {
            _db.Channels.Remove(channel);
            await _db.SaveChangesAsync();
        }
    }
}
