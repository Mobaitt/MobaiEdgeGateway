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
