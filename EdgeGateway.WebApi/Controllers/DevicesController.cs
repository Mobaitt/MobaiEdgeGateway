using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.WebApi.DTOs.Request;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// 设备管理接口
/// 提供设备的增删改查操作
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly DeviceManagementService _deviceService;
    private readonly RealtimeDataService _realtimeDataService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(DeviceManagementService deviceService, RealtimeDataService realtimeDataService, ILogger<DevicesController> logger)
    {
        _deviceService     = deviceService;
        _realtimeDataService = realtimeDataService;
        _logger            = logger;
    }

    /// <summary>获取所有设备列表</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DeviceListItem>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _deviceService.GetAllDevicesAsync();
        var result  = devices.Select(MapToListItem).ToList();
        return Ok(ApiResponse<List<DeviceListItem>>.Ok(result, $"共 {result.Count} 台设备"));
    }

    /// <summary>根据ID获取设备详情</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetById(int id)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"设备 ID={id} 不存在"));

        return Ok(ApiResponse<DeviceResponse>.Ok(MapToDetail(device)));
    }

    /// <summary>新增设备</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest req)
    {
        var device = new Device
        {
            Name              = req.Name,
            Code              = req.Code,
            Description       = req.Description,
            Protocol          = req.Protocol,
            Address           = req.Address,
            Port              = req.Port,
            PollingIntervalMs = req.PollingIntervalMs,
            IsEnabled         = req.IsEnabled
        };

        var created = await _deviceService.CreateDeviceAsync(device);
        _logger.LogInformation("新增设备: {Name} (ID={Id})", created.Name, created.Id);

        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            ApiResponse<DeviceResponse>.Ok(MapToDetail(created), "设备创建成功"));
    }

    /// <summary>更新设备信息</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceRequest req)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"设备 ID={id} 不存在"));

        device.Name              = req.Name;
        device.Description       = req.Description;
        device.Address           = req.Address;
        device.Port              = req.Port;
        device.PollingIntervalMs = req.PollingIntervalMs;
        device.IsEnabled         = req.IsEnabled;

        await _deviceService.UpdateDeviceAsync(device);
        return Ok(ApiResponse.Ok("设备更新成功"));
    }

    /// <summary>删除设备（级联删除数据点和映射）</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"设备 ID={id} 不存在"));

        await _deviceService.DeleteDeviceAsync(id);
        _logger.LogInformation("删除设备: {Name} (ID={Id})", device.Name, id);
        return Ok(ApiResponse.Ok("设备删除成功"));
    }

    /// <summary>启用/禁用设备</summary>
    [HttpPatch("{id:int}/toggle")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Toggle(int id)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"设备 ID={id} 不存在"));

        if (device.IsEnabled)
        {
            // 当前是启用状态，执行禁用操作 -> 停止采集
            await _deviceService.DisableDeviceAsync(id);
            _deviceService.StopDeviceCollection(id);
            _logger.LogInformation("设备已禁用：{Name} (ID={Id})", device.Name, id);
            return Ok(ApiResponse.Ok("设备已禁用，采集任务已停止"));
        }
        else
        {
            // 当前是禁用状态，执行启用操作 -> 启动采集
            await _deviceService.EnableDeviceAsync(id);
            await _deviceService.StartDeviceCollectionAsync(id);
            _logger.LogInformation("设备已启用：{Name} (ID={Id})", device.Name, id);
            return Ok(ApiResponse.Ok("设备已启用，采集任务已启动"));
        }
    }

    // =============================================
    // 数据点子路由（归属于设备下）
    // =============================================

    /// <summary>获取设备下的所有数据点</summary>
    [HttpGet("{deviceId:int}/datapoints")]
    [ProducesResponseType(typeof(ApiResponse<List<DataPointResponse>>), 200)]
    public async Task<IActionResult> GetDataPoints(int deviceId)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
            return NotFound(ApiResponse.Fail($"设备 ID={deviceId} 不存在"));

        var dataPoints = await _deviceService.GetDataPointsAsync(deviceId);
        var result = dataPoints.Select(dp => MapDataPointToResponse(dp, device.Name)).ToList();

        return Ok(ApiResponse<List<DataPointResponse>>.Ok(result, $"共 {result.Count} 个数据点"));
    }

    /// <summary>获取设备数据点的实时数据</summary>
    [HttpGet("{deviceId:int}/datapoints/realtime")]
    [ProducesResponseType(typeof(ApiResponse<List<DataPointRealtimeResponse>>), 200)]
    public IActionResult GetDeviceRealtimeData(int deviceId)
    {
        var realtimeData = _realtimeDataService.GetDeviceData(deviceId);
        var result = realtimeData.Select(d => new DataPointRealtimeResponse
        {
            DataPointId = d.DataPointId,
            Value       = d.Value,
            Quality     = d.Quality.ToString(),
            Timestamp   = d.Timestamp
        }).ToList();

        return Ok(ApiResponse<List<DataPointRealtimeResponse>>.Ok(result, $"共 {result.Count} 个实时数据"));
    }

    /// <summary>为设备添加数据点</summary>
    [HttpPost("{deviceId:int}/datapoints")]
    [ProducesResponseType(typeof(ApiResponse<DataPointResponse>), 201)]
    public async Task<IActionResult> CreateDataPoint(int deviceId, [FromBody] CreateDataPointRequest req)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
            return NotFound(ApiResponse.Fail($"设备 ID={deviceId} 不存在"));

        var dataPoint = new Domain.Entities.DataPoint
        {
            DeviceId    = deviceId,
            Name        = req.Name,
            Tag         = req.Tag,
            Description = req.Description,
            Address     = req.Address,
            DataType    = req.DataType,
            Unit        = req.Unit,
            IsEnabled   = req.IsEnabled
        };

        var created = await _deviceService.CreateDataPointAsync(dataPoint);
        return CreatedAtAction(nameof(GetDataPoints), new { deviceId },
            ApiResponse<DataPointResponse>.Ok(MapDataPointToResponse(created, device.Name), "数据点创建成功"));
    }

    /// <summary>删除数据点</summary>
    [HttpDelete("{deviceId:int}/datapoints/{dataPointId:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> DeleteDataPoint(int deviceId, int dataPointId)
    {
        await _deviceService.DeleteDataPointAsync(dataPointId);
        return Ok(ApiResponse.Ok("数据点删除成功"));
    }

    // ==================== 映射方法 ====================

    private static DeviceListItem MapToListItem(Device d) => new()
    {
        Id             = d.Id,
        Name           = d.Name,
        Code           = d.Code,
        Protocol       = d.Protocol.ToString(),
        Address        = d.Address,
        IsEnabled      = d.IsEnabled,
        DataPointCount = d.DataPoints.Count
    };

    private static DeviceResponse MapToDetail(Device d) => new()
    {
        Id                = d.Id,
        Name              = d.Name,
        Code              = d.Code,
        Description       = d.Description,
        Protocol          = d.Protocol.ToString(),
        ProtocolValue     = (int)d.Protocol,
        Address           = d.Address,
        Port              = d.Port,
        IsEnabled         = d.IsEnabled,
        PollingIntervalMs = d.PollingIntervalMs,
        DataPointCount    = d.DataPoints.Count,
        CreatedAt         = d.CreatedAt,
        UpdatedAt         = d.UpdatedAt
    };

    private static DataPointResponse MapDataPointToResponse(Domain.Entities.DataPoint dp, string deviceName) => new()
    {
        Id            = dp.Id,
        DeviceId      = dp.DeviceId,
        DeviceName    = deviceName,
        Name          = dp.Name,
        Tag           = dp.Tag,
        Description   = dp.Description,
        Address       = dp.Address,
        DataType      = dp.DataType.ToString(),
        DataTypeValue = (int)dp.DataType,
        Unit          = dp.Unit,
        IsEnabled     = dp.IsEnabled,
        CreatedAt     = dp.CreatedAt
    };
}
