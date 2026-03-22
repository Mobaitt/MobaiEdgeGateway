using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 设备管理服务 - 封装设备 CRUD 及数据点管理的业务逻辑
/// </summary>
public class DeviceManagementService
{
    private readonly IDeviceRepository _deviceRepo;
    private readonly IDataPointRepository _dataPointRepo;
    private readonly IChannelRepository _channelRepo;
    private readonly IChannelMappingRepository _mappingRepo;
    private readonly DataCollectionService _collectionService;
    private readonly DataSendService _sendService;
    private readonly VirtualNodeManagementService _virtualNodeService;
    private readonly ILogger<DeviceManagementService> _logger;

    public DeviceManagementService(
        IDeviceRepository deviceRepo,
        IDataPointRepository dataPointRepo,
        IChannelRepository channelRepo,
        IChannelMappingRepository mappingRepo,
        DataCollectionService collectionService,
        DataSendService sendService,
        VirtualNodeManagementService virtualNodeService,
        ILogger<DeviceManagementService> logger)
    {
        _deviceRepo         = deviceRepo;
        _dataPointRepo      = dataPointRepo;
        _channelRepo        = channelRepo;
        _mappingRepo        = mappingRepo;
        _collectionService  = collectionService;
        _sendService        = sendService;
        _virtualNodeService = virtualNodeService;
        _logger             = logger;
    }

    // ==================== 设备管理 ====================

    /// <summary>获取所有设备（含数据点）</summary>
    public Task<IEnumerable<Device>> GetAllDevicesAsync() =>
        _deviceRepo.GetAllAsync();

    /// <summary>获取单个设备详情</summary>
    public Task<Device?> GetDeviceAsync(int id) =>
        _deviceRepo.GetByIdAsync(id);

    /// <summary>新增设备</summary>
    public async Task<Device> CreateDeviceAsync(Device device)
    {
        // 验证设备编码唯一性
        var existingDevices = await _deviceRepo.GetAllAsync();
        if (existingDevices.Any(d => d.Code == device.Code))
        {
            throw new InvalidOperationException($"设备编码 '{device.Code}' 已存在，请使用其他编码");
        }

        var created = await _deviceRepo.AddAsync(device);
        _logger.LogInformation("新增设备成功：{DeviceName} (ID={Id})", created.Name, created.Id);
        return created;
    }

    /// <summary>更新设备信息</summary>
    public async Task UpdateDeviceAsync(Device device)
    {
        // 验证设备编码唯一性（排除当前设备）
        var existingDevices = await _deviceRepo.GetAllAsync();
        if (existingDevices.Any(d => d.Code == device.Code && d.Id != device.Id))
        {
            throw new InvalidOperationException($"设备编码 '{device.Code}' 已存在，请使用其他编码");
        }

        await _deviceRepo.UpdateAsync(device);
        _logger.LogInformation("更新设备：{DeviceName} (ID={Id})", device.Name, device.Id);
    }

    /// <summary>启用设备采集</summary>
    public async Task EnableDeviceAsync(int deviceId)
    {
        var device = await _deviceRepo.GetByIdAsync(deviceId);
        if (device == null)
            throw new InvalidOperationException($"设备 ID={deviceId} 不存在");

        device.IsEnabled = true;
        await _deviceRepo.UpdateAsync(device);
        _logger.LogInformation("启用设备采集：{DeviceName} (ID={Id})", device.Name, deviceId);
    }

    /// <summary>禁用设备采集</summary>
    public async Task DisableDeviceAsync(int deviceId)
    {
        var device = await _deviceRepo.GetByIdAsync(deviceId);
        if (device == null)
            throw new InvalidOperationException($"设备 ID={deviceId} 不存在");

        device.IsEnabled = false;
        await _deviceRepo.UpdateAsync(device);
        _logger.LogInformation("禁用设备采集：{DeviceName} (ID={Id})", device.Name, deviceId);
    }

    /// <summary>删除设备（级联删除数据点和映射关系）</summary>
    public async Task DeleteDeviceAsync(int id)
    {
        await _deviceRepo.DeleteAsync(id);
        _logger.LogInformation("删除设备 ID={Id}", id);
    }

    // ==================== 数据点管理 ====================

    /// <summary>获取设备下的所有数据点</summary>
    public Task<IEnumerable<DataPoint>> GetDataPointsAsync(int deviceId) =>
        _dataPointRepo.GetByDeviceIdAsync(deviceId);

    /// <summary>分页查询设备数据点</summary>
    public Task<(List<DataPoint> Items, int Total)> GetPagedDataPointsAsync(int deviceId, int page, int pageSize, string? search = null, int? dataType = null, bool? isEnabled = null) =>
        _dataPointRepo.GetPagedByDeviceIdAsync(deviceId, page, pageSize, search, dataType, isEnabled);

    /// <summary>获取单个数据点</summary>
    public Task<DataPoint?> GetDataPointAsync(int id) =>
        _dataPointRepo.GetByIdAsync(id);

    /// <summary>新增数据点</summary>
    public async Task<DataPoint> CreateDataPointAsync(DataPoint dataPoint)
    {
        // 验证同一设备下 Tag 唯一性
        var existingPoints = await _dataPointRepo.GetByDeviceIdAsync(dataPoint.DeviceId);
        if (existingPoints.Any(p => p.Tag == dataPoint.Tag))
        {
            throw new InvalidOperationException($"设备下已存在 Tag 为 '{dataPoint.Tag}' 的数据点");
        }

        var created = await _dataPointRepo.AddAsync(dataPoint);
        _logger.LogInformation("新增数据点：{Tag} (设备 ID={DeviceId})", created.Tag, created.DeviceId);
        return created;
    }

    /// <summary>更新数据点</summary>
    public async Task UpdateDataPointAsync(DataPoint dataPoint)
    {
        // 验证同一设备下 Tag 唯一性（排除当前数据点）
        var existingPoints = await _dataPointRepo.GetByDeviceIdAsync(dataPoint.DeviceId);
        if (existingPoints.Any(p => p.Tag == dataPoint.Tag && p.Id != dataPoint.Id))
        {
            throw new InvalidOperationException($"设备下已存在 Tag 为 '{dataPoint.Tag}' 的数据点");
        }

        await _dataPointRepo.UpdateAsync(dataPoint);
        _logger.LogInformation("更新数据点：{Tag} (ID={Id})", dataPoint.Tag, dataPoint.Id);
    }

    /// <summary>删除数据点</summary>
    public async Task DeleteDataPointAsync(int dataPointId)
    {
        await _dataPointRepo.DeleteAsync(dataPointId);
        _logger.LogInformation("删除数据点 ID={Id}", dataPointId);
    }

    // ==================== 通道映射管理 ====================

    /// <summary>
    /// 将数据点绑定到发送通道（建立映射关系）
    /// 采集的数据将通过该通道发送出去
    /// </summary>
    /// <param name="channelId">目标发送通道 ID</param>
    /// <param name="dataPointIds">要绑定的数据点 ID 列表</param>
    public async Task BindDataPointsToChannelAsync(int channelId, IEnumerable<int> dataPointIds)
    {
        var channel = await _channelRepo.GetByIdAsync(channelId)
            ?? throw new InvalidOperationException($"通道 ID={channelId} 不存在");

        var existingMappings = (await _mappingRepo.GetByChannelIdAsync(channelId))
            .Select(m => m.DataPointId)
            .ToHashSet();

        // 仅添加尚未绑定的数据点（避免重复）
        var newMappings = dataPointIds
            .Where(id => !existingMappings.Contains(id))
            .Select(id => new ChannelDataPointMapping
            {
                ChannelId   = channelId,
                DataPointId = id,
                IsEnabled   = true
            })
            .ToList();

        if (newMappings.Any())
        {
            await _mappingRepo.AddRangeAsync(newMappings);
            _logger.LogInformation("通道 [{ChannelName}] 成功绑定 {Count} 个数据点",
                channel.Name, newMappings.Count);
        }
    }

    /// <summary>
    /// 将虚拟数据点绑定到发送通道（建立映射关系）
    /// 虚拟数据点计算后的数据将通过该通道发送出去
    /// </summary>
    /// <param name="channelId">目标发送通道 ID</param>
    /// <param name="virtualDataPointIds">要绑定的虚拟数据点 ID 列表</param>
    public async Task BindVirtualDataPointsToChannelAsync(int channelId, IEnumerable<int> virtualDataPointIds)
    {
        var channel = await _channelRepo.GetByIdAsync(channelId)
            ?? throw new InvalidOperationException($"通道 ID={channelId} 不存在");

        var existingMappings = (await _mappingRepo.GetByChannelIdAsync(channelId))
            .Select(m => m.VirtualDataPointId)
            .ToHashSet();

        // 获取所有虚拟点位用于构建完整 Tag
        var allVirtualPoints = await _virtualNodeService.GetAllVirtualDataPointsAsync();
        var virtualPointDict = allVirtualPoints.ToDictionary(vp => vp.Id);

        // 仅添加尚未绑定的虚拟数据点（避免重复）
        var newMappings = virtualDataPointIds
            .Where(id => !existingMappings.Contains(id) && id != 0)
            .Select(id =>
            {
                var vp = virtualPointDict.TryGetValue(id, out var point) ? point : null;
                return new ChannelDataPointMapping
                {
                    ChannelId          = channelId,
                    VirtualDataPointId = id,
                    DataPointTag       = vp != null ? $"{vp.Device?.Code}.{vp.Tag}" : null,
                    DataPointName      = vp != null ? vp.Name : null,
                    IsEnabled          = true
                };
            })
            .ToList();

        if (newMappings.Any())
        {
            await _mappingRepo.AddRangeAsync(newMappings);
            await _sendService.RefreshChannelsCacheForceAsync();
            _logger.LogInformation("通道 [{ChannelName}] 成功绑定 {Count} 个虚拟数据点",
                channel.Name, newMappings.Count);
        }
    }

    /// <summary>获取所有发送通道</summary>
    public Task<IEnumerable<Channel>> GetAllChannelsAsync() =>
        _channelRepo.GetAllAsync();

    /// <summary>新增发送通道</summary>
    public async Task<Channel> CreateChannelAsync(Channel channel)
    {
        var created = await _channelRepo.AddAsync(channel);
        _logger.LogInformation("新增发送通道：{ChannelName}", created.Name);
        return created;
    }

    /// <summary>获取通道的所有数据点映射关系（含导航属性）</summary>
    public Task<IEnumerable<ChannelDataPointMapping>> GetChannelMappingsAsync(int channelId) =>
        _mappingRepo.GetByChannelIdAsync(channelId);

    /// <summary>分页查询通道映射</summary>
    public Task<(List<ChannelDataPointMapping> Items, int Total)> GetPagedChannelMappingsAsync(int channelId, int page, int pageSize, string? search = null, bool? isEnabled = null, bool? isVirtual = null) =>
        _mappingRepo.GetPagedByChannelIdAsync(channelId, page, pageSize, search, isEnabled, isVirtual);

    /// <summary>删除映射关系</summary>
    public async Task DeleteMappingAsync(int mappingId)
    {
        await _mappingRepo.DeleteAsync(mappingId);
        _logger.LogInformation("删除映射关系 ID={Id}", mappingId);
    }

    /// <summary>启用发送通道</summary>
    public async Task EnableChannelAsync(int channelId)
    {
        var channel = await _channelRepo.GetByIdAsync(channelId);
        if (channel == null)
            throw new InvalidOperationException($"通道 ID={channelId} 不存在");

        channel.IsEnabled = true;
        await _channelRepo.UpdateAsync(channel);
        
        // 动态启用发送服务
        await _sendService.EnableChannelAsync(channelId);
        
        _logger.LogInformation("启用发送通道：{ChannelName} (ID={Id})", channel.Name, channelId);
    }

    /// <summary>停用发送通道</summary>
    public async Task DisableChannelAsync(int channelId)
    {
        var channel = await _channelRepo.GetByIdAsync(channelId);
        if (channel == null)
            throw new InvalidOperationException($"通道 ID={channelId} 不存在");

        channel.IsEnabled = false;
        await _channelRepo.UpdateAsync(channel);

        // 动态停用发送服务
        await _sendService.DisableChannelAsync(channelId);

        _logger.LogInformation("停用发送通道：{ChannelName} (ID={Id})", channel.Name, channelId);
    }

    /// <summary>更新发送通道</summary>
    public async Task UpdateChannelAsync(Channel channel)
    {
        // 在更新之前获取旧通道配置（用于对比 endpoint 变化）
        // 使用 AsNoTracking 确保获取的是原始值，不受 EF Core 跟踪影响
        var oldChannel = await _channelRepo.GetByIdNoTrackingAsync(channel.Id);
        var oldEndpoint = oldChannel?.Endpoint;

        await _channelRepo.UpdateAsync(channel);
        _logger.LogInformation("更新发送通道：{ChannelName} (ID={Id})", channel.Name, channel.Id);

        // 如果通道已启用，重新初始化发送策略
        if (channel.IsEnabled)
        {
            // 如果是 HTTP 服务端模式且路径变化，需要重新注册端点
            if (channel.Protocol == Domain.Enums.SendProtocol.Http)
            {
                await _sendService.ReconfigureChannelEndpointAsync(channel.Id, oldEndpoint);
            }
            else
            {
                await _sendService.DisableChannelAsync(channel.Id);
                await _sendService.EnableChannelAsync(channel.Id);
            }
        }
    }

    /// <summary>删除发送通道（级联删除映射关系）</summary>
    public async Task DeleteChannelAsync(int channelId)
    {
        // 先停用通道
        await _sendService.DisableChannelAsync(channelId);

        // 删除通道（EF Core 会级联删除映射关系）
        await _channelRepo.DeleteAsync(channelId);

        _logger.LogInformation("删除发送通道 ID={Id}", channelId);
    }

    /// <summary>启动设备采集</summary>
    public async Task StartDeviceCollectionAsync(int deviceId)
    {
        await _collectionService.StartDeviceTaskByIdAsync(deviceId);
    }

    /// <summary>停止设备采集</summary>
    public void StopDeviceCollection(int deviceId)
    {
        _collectionService.StopDevice(deviceId);
    }
}
