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
            .Include(m => m.VirtualDataPoint)
            .Where(m => m.ChannelId == channelId)
            .ToListAsync();

    /// <summary>分页查询通道映射</summary>
    public async Task<(List<ChannelDataPointMapping> Items, int Total)> GetPagedByChannelIdAsync(int channelId, int page, int pageSize, string? search = null, bool? isEnabled = null, bool? isVirtual = null)
    {
        var query = _db.ChannelDataPointMappings
            .Include(m => m.DataPoint)
            .Include(m => m.VirtualDataPoint)
            .Where(m => m.ChannelId == channelId)
            .AsQueryable();

        // 搜索过滤
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(m =>
                (m.DataPointTag != null && m.DataPointTag.Contains(search)) ||
                (m.DataPointName != null && m.DataPointName.Contains(search)) ||
                (m.DataPoint!.Tag.Contains(search)) ||
                (m.DataPoint.Name.Contains(search)) ||
                (m.VirtualDataPoint!.Tag.Contains(search)) ||
                (m.VirtualDataPoint.Name.Contains(search)));
        }

        // 启用状态过滤
        if (isEnabled.HasValue)
        {
            query = query.Where(m => m.IsEnabled == isEnabled.Value);
        }

        // 虚拟数据点过滤
        if (isVirtual.HasValue)
        {
            if (isVirtual.Value)
            {
                query = query.Where(m => m.VirtualDataPointId.HasValue);
            }
            else
            {
                query = query.Where(m => !m.VirtualDataPointId.HasValue);
            }
        }

        // 获取总数
        var total = await query.CountAsync();

        // 分页查询
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

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
