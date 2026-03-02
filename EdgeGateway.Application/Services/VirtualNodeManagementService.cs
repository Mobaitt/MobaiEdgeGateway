using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 虚拟节点管理服务 - 负责虚拟数据点的 CRUD
/// 虚拟节点依附于普通设备，可以像普通数据点一样被管理和发送
/// </summary>
public class VirtualNodeManagementService
{
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly IVirtualNodeEngine _virtualNodeEngine;
    private readonly ILogger<VirtualNodeManagementService> _logger;

    public VirtualNodeManagementService(
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        IVirtualNodeEngine virtualNodeEngine,
        ILogger<VirtualNodeManagementService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _virtualNodeEngine = virtualNodeEngine;
        _logger = logger;
    }

    #region 虚拟数据点管理

    /// <summary>
    /// 获取所有虚拟数据点
    /// </summary>
    public async Task<List<VirtualDataPoint>> GetAllVirtualDataPointsAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.VirtualDataPoints
            .Include(vp => vp.Device)
            .OrderBy(vp => vp.Tag)
            .ToListAsync();
    }

    /// <summary>
    /// 获取指定设备下的所有虚拟数据点
    /// </summary>
    public async Task<List<VirtualDataPoint>> GetVirtualDataPointsByDeviceIdAsync(int deviceId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.VirtualDataPoints
            .Where(vp => vp.DeviceId == deviceId)
            .OrderBy(vp => vp.Tag)
            .ToListAsync();
    }

    /// <summary>
    /// 根据 ID 获取虚拟数据点
    /// </summary>
    public async Task<VirtualDataPoint?> GetVirtualDataPointByIdAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.VirtualDataPoints
            .Include(vp => vp.Device)
            .FirstOrDefaultAsync(vp => vp.Id == id);
    }

    /// <summary>
    /// 创建虚拟数据点
    /// </summary>
    public async Task<VirtualDataPoint> CreateVirtualDataPointAsync(VirtualDataPoint dataPoint)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        // 检查 Tag 是否重复
        if (await context.VirtualDataPoints.AnyAsync(vp => vp.Tag == dataPoint.Tag))
            throw new InvalidOperationException($"虚拟数据点 Tag {dataPoint.Tag} 已存在");

        // 检查所属设备是否存在
        var device = await context.Devices.FindAsync(dataPoint.DeviceId);
        if (device == null)
            throw new InvalidOperationException($"设备 ID={dataPoint.DeviceId} 不存在");

        // 解析依赖 Tags
        var dependencies = _virtualNodeEngine.ParseDependencies(dataPoint.Expression);
        dataPoint.DependencyTags = JsonConvert.SerializeObject(dependencies);

        dataPoint.CreatedAt = DateTime.UtcNow;

        context.VirtualDataPoints.Add(dataPoint);
        await context.SaveChangesAsync();

        // 刷新引擎缓存
        await _virtualNodeEngine.RefreshCacheAsync();

        _logger.LogInformation("虚拟数据点 [{PointName}] 创建成功，ID={PointId}, Tag={Tag}",
            dataPoint.Name, dataPoint.Id, dataPoint.Tag);
        return dataPoint;
    }

    /// <summary>
    /// 更新虚拟数据点
    /// </summary>
    public async Task<VirtualDataPoint> UpdateVirtualDataPointAsync(VirtualDataPoint dataPoint)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var existing = await context.VirtualDataPoints.FindAsync(dataPoint.Id);
        if (existing == null)
            throw new InvalidOperationException($"虚拟数据点 ID={dataPoint.Id} 不存在");

        // 检查 Tag 是否重复（排除自身）
        if (await context.VirtualDataPoints.AnyAsync(vp => vp.Tag == dataPoint.Tag && vp.Id != dataPoint.Id))
            throw new InvalidOperationException($"虚拟数据点 Tag {dataPoint.Tag} 已存在");

        // 解析依赖 Tags
        var dependencies = _virtualNodeEngine.ParseDependencies(dataPoint.Expression);
        dataPoint.DependencyTags = JsonConvert.SerializeObject(dependencies);

        existing.Name = dataPoint.Name;
        existing.Tag = dataPoint.Tag;
        existing.Description = dataPoint.Description;
        existing.Expression = dataPoint.Expression;
        existing.CalculationType = dataPoint.CalculationType;
        existing.DataType = dataPoint.DataType;
        existing.Unit = dataPoint.Unit;
        existing.IsEnabled = dataPoint.IsEnabled;
        existing.DependencyTags = dataPoint.DependencyTags;

        await context.SaveChangesAsync();

        // 刷新引擎缓存
        await _virtualNodeEngine.RefreshCacheAsync();

        _logger.LogInformation("虚拟数据点 [{PointName}] 更新成功，ID={PointId}, Tag={Tag}",
            dataPoint.Name, dataPoint.Id, dataPoint.Tag);
        return existing;
    }

    /// <summary>
    /// 删除虚拟数据点
    /// </summary>
    public async Task DeleteVirtualDataPointAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var dataPoint = await context.VirtualDataPoints.FindAsync(id);
        if (dataPoint == null)
            throw new InvalidOperationException($"虚拟数据点 ID={id} 不存在");

        context.VirtualDataPoints.Remove(dataPoint);
        await context.SaveChangesAsync();

        // 刷新引擎缓存
        await _virtualNodeEngine.RefreshCacheAsync();

        _logger.LogInformation("虚拟数据点 [{PointName}] 删除成功，ID={PointId}", dataPoint.Name, dataPoint.Id);
    }

    /// <summary>
    /// 触发单个虚拟数据点计算
    /// </summary>
    public async Task<VirtualNodeCalculationResult> CalculateVirtualDataPointAsync(int id)
    {
        var dataPoint = await GetVirtualDataPointByIdAsync(id);
        if (dataPoint == null)
            throw new InvalidOperationException($"虚拟数据点 ID={id} 不存在");

        var result = await _virtualNodeEngine.CalculateAsync(dataPoint);
        _logger.LogInformation("虚拟数据点 [{PointName}] 计算完成：{Value}", dataPoint.Name, result.Value);
        return result;
    }

    /// <summary>
    /// 计算指定设备下的所有启用的虚拟数据点
    /// </summary>
    public async Task<List<VirtualNodeCalculationResult>> CalculateDeviceVirtualDataPointsAsync(int deviceId)
    {
        var results = await _virtualNodeEngine.CalculateDeviceAsync(deviceId);
        _logger.LogInformation("设备 ID={DeviceId} 虚拟节点计算完成，成功 {SuccessCount}/{TotalCount}",
            deviceId, results.Count(r => r.Success), results.Count);
        return results;
    }

    /// <summary>
    /// 计算所有启用的虚拟数据点
    /// </summary>
    public async Task<List<VirtualNodeCalculationResult>> CalculateAllVirtualDataPointsAsync()
    {
        var results = await _virtualNodeEngine.CalculateAllAsync();
        _logger.LogInformation("虚拟节点计算完成，成功 {SuccessCount}/{TotalCount}",
            results.Count(r => r.Success), results.Count);
        return results;
    }

    /// <summary>
    /// 解析表达式中的依赖 Tags
    /// </summary>
    public List<string> ParseExpressionDependencies(string expression)
    {
        return _virtualNodeEngine.ParseDependencies(expression);
    }

    #endregion
}
