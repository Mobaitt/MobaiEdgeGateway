using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EdgeGateway.Infrastructure.Repositories;

/// <summary>
/// 通道-数据点映射关系仓储实现（EF Core + SQLite）
/// </summary>
public class ChannelMappingRepository : IChannelMappingRepository
{
    private readonly GatewayDbContext _db;

    public ChannelMappingRepository(GatewayDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IEnumerable<ChannelDataPointMapping>> GetByChannelIdAsync(int channelId) =>
        await _db.ChannelDataPointMappings
            .Include(m => m.DataPoint)
            .Where(m => m.ChannelId == channelId)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<ChannelDataPointMapping>> GetByDataPointIdAsync(int dataPointId) =>
        await _db.ChannelDataPointMappings
            .Include(m => m.Channel)
            .Where(m => m.DataPointId == dataPointId)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<ChannelDataPointMapping> AddAsync(ChannelDataPointMapping mapping)
    {
        mapping.CreatedAt = DateTime.UtcNow;
        _db.ChannelDataPointMappings.Add(mapping);
        await _db.SaveChangesAsync();
        return mapping;
    }

    /// <inheritdoc/>
    public async Task AddRangeAsync(IEnumerable<ChannelDataPointMapping> mappings)
    {
        var list = mappings.ToList();
        list.ForEach(m => m.CreatedAt = DateTime.UtcNow);
        await _db.ChannelDataPointMappings.AddRangeAsync(list);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        var mapping = await _db.ChannelDataPointMappings.FindAsync(id);
        if (mapping != null)
        {
            _db.ChannelDataPointMappings.Remove(mapping);
            await _db.SaveChangesAsync();
        }
    }
}
