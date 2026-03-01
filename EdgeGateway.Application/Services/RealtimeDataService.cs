using System.Collections.Concurrent;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 实时数据服务 - 存储和提供设备采集的最新数据
/// 使用内存缓存最新的采集值，供前端实时展示使用
/// </summary>
public class RealtimeDataService
{
    /// <summary>
    /// 存储每个数据点的最新采集值
    /// Key: 数据点 ID, Value: 最新采集数据
    /// </summary>
    private readonly ConcurrentDictionary<int, CollectedData> _latestValues = new();

    /// <summary>
    /// 存储每个设备最后更新时间
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _deviceLastUpdate = new();

    private readonly ILogger<RealtimeDataService> _logger;

    public RealtimeDataService(ILogger<RealtimeDataService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 更新一批采集数据
    /// </summary>
    public void UpdateData(IEnumerable<CollectedData> dataList)
    {
        var count = 0;
        foreach (var data in dataList)
        {
            _latestValues[data.DataPointId] = data;
            
            // 更新设备最后更新时间（通过 CollectedData 中的 DeviceId）
            if (!_deviceLastUpdate.ContainsKey(data.DeviceId))
            {
                _deviceLastUpdate[data.DeviceId] = DateTime.UtcNow;
            }
            _deviceLastUpdate[data.DeviceId] = DateTime.UtcNow;
            
            count++;
        }
        
        if (count > 0)
        {
            _logger.LogDebug("实时数据服务更新：{Count} 个数据点", count);
        }
    }

    /// <summary>
    /// 获取指定设备的所有最新采集数据
    /// </summary>
    public List<CollectedData> GetDeviceData(int deviceId)
    {
        return _latestValues
            .Where(kvp => kvp.Value.DeviceId == deviceId)
            .Select(kvp => kvp.Value)
            .ToList();
    }

    /// <summary>
    /// 获取指定数据点的最新值
    /// </summary>
    public CollectedData? GetDataPointValue(int dataPointId)
    {
        _latestValues.TryGetValue(dataPointId, out var value);
        return value;
    }

    /// <summary>
    /// 获取设备最后更新时间
    /// </summary>
    public DateTime? GetDeviceLastUpdateTime(int deviceId)
    {
        _deviceLastUpdate.TryGetValue(deviceId, out var time);
        return time;
    }

    /// <summary>
    /// 清除指定设备的数据
    /// </summary>
    public void ClearDeviceData(int deviceId)
    {
        var keysToRemove = _latestValues
            .Where(kvp => kvp.Value.DeviceId == deviceId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _latestValues.TryRemove(key, out _);
        }
        
        _deviceLastUpdate.TryRemove(deviceId, out _);
        
        _logger.LogInformation("已清除设备 ID={DeviceId} 的实时数据", deviceId);
    }
}
