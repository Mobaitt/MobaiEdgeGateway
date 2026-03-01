using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EdgeGateway.Infrastructure.Repositories;

/// <summary>
/// 设备仓储实现（EF Core + SQLite）
/// </summary>
public class DeviceRepository : IDeviceRepository
{
    private readonly GatewayDbContext _db;

    public DeviceRepository(GatewayDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<Device?> GetByIdAsync(int id) =>
        await _db.Devices
            .Include(d => d.DataPoints)
            .FirstOrDefaultAsync(d => d.Id == id);

    /// <inheritdoc/>
    public async Task<IEnumerable<Device>> GetAllAsync() =>
        await _db.Devices
            .Include(d => d.DataPoints)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Device>> GetEnabledAsync() =>
        await _db.Devices
            .Include(d => d.DataPoints.Where(dp => dp.IsEnabled))
            .Where(d => d.IsEnabled)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Device> AddAsync(Device device)
    {
        device.CreatedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return device;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Device device)
    {
        device.UpdatedAt = DateTime.UtcNow;
        _db.Devices.Update(device);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device != null)
        {
            _db.Devices.Remove(device);
            await _db.SaveChangesAsync();
        }
    }
}
