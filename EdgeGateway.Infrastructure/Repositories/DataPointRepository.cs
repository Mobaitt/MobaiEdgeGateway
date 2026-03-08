using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EdgeGateway.Infrastructure.Repositories;

/// <summary>
/// 数据点仓储实现（EF Core + SQLite）
/// </summary>
public class DataPointRepository : IDataPointRepository
{
    private readonly GatewayDbContext _db;

    public DataPointRepository(GatewayDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<DataPoint?> GetByIdAsync(int id) =>
        await _db.DataPoints.FindAsync(id);

    /// <inheritdoc/>
    public async Task<IEnumerable<DataPoint>> GetByDeviceIdAsync(int deviceId) =>
        await _db.DataPoints
            .Where(dp => dp.DeviceId == deviceId)
            .ToListAsync();

    /// <summary>分页查询设备数据点</summary>
    public async Task<(List<DataPoint> Items, int Total)> GetPagedByDeviceIdAsync(int deviceId, int page, int pageSize, string? search = null, int? dataType = null, bool? isEnabled = null)
    {
        var query = _db.DataPoints
            .Where(dp => dp.DeviceId == deviceId)
            .AsQueryable();

        // 搜索过滤
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(dp =>
                dp.Tag.Contains(search) ||
                dp.Name.Contains(search) ||
                dp.Address.Contains(search) ||
                dp.Description!.Contains(search));
        }

        // 数据类型过滤
        if (dataType.HasValue)
        {
            query = query.Where(dp => (int)dp.DataType == dataType.Value);
        }

        // 启用状态过滤
        if (isEnabled.HasValue)
        {
            query = query.Where(dp => dp.IsEnabled == isEnabled.Value);
        }

        // 获取总数
        var total = await query.CountAsync();

        // 分页查询
        var items = await query
            .OrderByDescending(dp => dp.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<DataPoint> AddAsync(DataPoint dataPoint)
    {
        dataPoint.CreatedAt = DateTime.UtcNow;
        _db.DataPoints.Add(dataPoint);
        await _db.SaveChangesAsync();
        return dataPoint;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(DataPoint dataPoint)
    {
        _db.DataPoints.Update(dataPoint);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        var dp = await _db.DataPoints.FindAsync(id);
        if (dp != null)
        {
            _db.DataPoints.Remove(dp);
            await _db.SaveChangesAsync();
        }
    }
}
