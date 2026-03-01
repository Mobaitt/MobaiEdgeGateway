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
            .FirstOrDefaultAsync(c => c.Id == id);

    /// <inheritdoc/>
    public async Task<IEnumerable<Channel>> GetAllAsync() =>
        await _db.Channels.ToListAsync();

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
    public async Task<IEnumerable<Channel>> GetEnabledWithMappingsAsync() =>
        await _db.Channels
            .Where(c => c.IsEnabled)
            .Include(c => c.DataPointMappings.Where(m => m.IsEnabled))
                .ThenInclude(m => m.DataPoint)
            .ToListAsync();

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
