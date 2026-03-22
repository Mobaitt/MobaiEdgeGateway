using System.Text.Json;
using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.WebApi.DTOs.Request;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// Device management endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly DeviceManagementService _deviceService;
    private readonly DataCollectionService _collectionService;
    private readonly DataPointControlService _dataPointControlService;
    private readonly VirtualNodeManagementService _virtualNodeService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        DeviceManagementService deviceService,
        DataCollectionService collectionService,
        DataPointControlService dataPointControlService,
        VirtualNodeManagementService virtualNodeService,
        ILogger<DevicesController> logger)
    {
        _deviceService = deviceService;
        _collectionService = collectionService;
        _dataPointControlService = dataPointControlService;
        _virtualNodeService = virtualNodeService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DeviceListItem>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _deviceService.GetAllDevicesAsync();
        var allVirtualPoints = await _virtualNodeService.GetAllVirtualDataPointsAsync();

        var virtualPointCounts = allVirtualPoints
            .GroupBy(vp => vp.DeviceId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = devices
            .Select(d => MapToListItem(d, virtualPointCounts.GetValueOrDefault(d.Id, 0)))
            .ToList();

        return Ok(ApiResponse<List<DeviceListItem>>.Ok(result, $"Loaded {result.Count} devices"));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetById(int id)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={id} was not found"));

        var virtualPointCount = (await _virtualNodeService.GetVirtualDataPointsByDeviceIdAsync(id)).Count;
        return Ok(ApiResponse<DeviceResponse>.Ok(MapToDetail(device, virtualPointCount)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest req)
    {
        var device = new Device
        {
            Name = req.Name,
            Code = req.Code,
            Description = req.Description,
            Protocol = req.Protocol,
            Address = req.Address,
            Port = req.Port,
            PollingIntervalMs = req.PollingIntervalMs,
            IsEnabled = req.IsEnabled
        };

        var created = await _deviceService.CreateDeviceAsync(device);
        _logger.LogInformation("Created device: {Name} (ID={Id})", created.Name, created.Id);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            ApiResponse<DeviceResponse>.Ok(MapToDetail(created, 0), "Device created"));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceRequest req)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={id} was not found"));

        device.Name = req.Name;
        device.Description = req.Description;
        device.Address = req.Address;
        device.Port = req.Port;
        device.PollingIntervalMs = req.PollingIntervalMs;
        device.IsEnabled = req.IsEnabled;

        await _deviceService.UpdateDeviceAsync(device);
        return Ok(ApiResponse.Ok("Device updated"));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={id} was not found"));

        await _deviceService.DeleteDeviceAsync(id);
        _logger.LogInformation("Deleted device: {Name} (ID={Id})", device.Name, id);
        return Ok(ApiResponse.Ok("Device deleted"));
    }

    [HttpPatch("{id:int}/toggle")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Toggle(int id)
    {
        var device = await _deviceService.GetDeviceAsync(id);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={id} was not found"));

        if (device.IsEnabled)
        {
            await _deviceService.DisableDeviceAsync(id);
            _deviceService.StopDeviceCollection(id);
            _logger.LogInformation("Disabled device: {Name} (ID={Id})", device.Name, id);
            return Ok(ApiResponse.Ok("Device disabled and collection stopped"));
        }

        await _deviceService.EnableDeviceAsync(id);
        await _deviceService.StartDeviceCollectionAsync(id);
        _logger.LogInformation("Enabled device: {Name} (ID={Id})", device.Name, id);
        return Ok(ApiResponse.Ok("Device enabled and collection started"));
    }

    [HttpGet("datapoints")]
    [ProducesResponseType(typeof(ApiResponse<List<DataPointResponse>>), 200)]
    public async Task<IActionResult> GetAllDataPoints()
    {
        var devices = await _deviceService.GetAllDevicesAsync();
        var allDataPoints = devices
            .SelectMany(d => d.DataPoints.Select(dp => MapDataPointToResponse(dp, d.Name)))
            .ToList();

        return Ok(ApiResponse<List<DataPointResponse>>.Ok(allDataPoints, $"Loaded {allDataPoints.Count} data points"));
    }

    [HttpGet("{deviceId:int}/datapoints/paged")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<DataPointResponse>>), 200)]
    public async Task<IActionResult> GetDataPointsPaged(
        int deviceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? dataType = null,
        [FromQuery] bool? isEnabled = null)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={deviceId} was not found"));

        var (items, total) = await _deviceService.GetPagedDataPointsAsync(deviceId, page, pageSize, search, dataType, isEnabled);
        var result = items.Select(dp => MapDataPointToResponse(dp, device.Name)).ToList();
        var pagedResult = PagedResponse<DataPointResponse>.Create(result, total, page, pageSize);

        return Ok(ApiResponse<PagedResponse<DataPointResponse>>.Ok(pagedResult, $"Loaded {total} data points"));
    }

    [HttpGet("{deviceId:int}/datapoints")]
    [ProducesResponseType(typeof(ApiResponse<List<DataPointResponse>>), 200)]
    public async Task<IActionResult> GetDataPoints(int deviceId)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={deviceId} was not found"));

        var dataPoints = await _deviceService.GetDataPointsAsync(deviceId);
        var result = dataPoints.Select(dp => MapDataPointToResponse(dp, device.Name)).ToList();

        return Ok(ApiResponse<List<DataPointResponse>>.Ok(result, $"Loaded {result.Count} data points"));
    }

    [HttpGet("{deviceId:int}/datapoints/realtime")]
    [ProducesResponseType(typeof(ApiResponse<List<DataPointRealtimeResponse>>), 200)]
    public IActionResult GetDeviceRealtimeData(int deviceId)
    {
        var realtimeData = _collectionService.GetDeviceSnapshotData(deviceId);
        var result = realtimeData.Select(d => new DataPointRealtimeResponse
        {
            DataPointId = d.DataPointId,
            Tag = d.Tag,
            Value = d.Value,
            Quality = d.Quality.ToString(),
            Timestamp = d.Timestamp
        }).ToList();

        return Ok(ApiResponse<List<DataPointRealtimeResponse>>.Ok(result, $"Loaded {result.Count} realtime values"));
    }

    [HttpPost("{deviceId:int}/datapoints")]
    [ProducesResponseType(typeof(ApiResponse<DataPointResponse>), 201)]
    public async Task<IActionResult> CreateDataPoint(int deviceId, [FromBody] CreateDataPointRequest req)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={deviceId} was not found"));

        var dataPoint = new DataPoint
        {
            DeviceId = deviceId,
            Name = req.Name,
            Tag = req.Tag,
            Description = req.Description,
            Address = req.Address,
            DataType = req.DataType,
            Unit = req.Unit,
            ModbusSlaveId = req.ModbusSlaveId,
            ModbusFunctionCode = req.ModbusFunctionCode,
            ModbusByteOrder = req.ModbusByteOrder,
            RegisterLength = req.RegisterLength,
            IsEnabled = req.IsEnabled,
            IsControllable = req.IsControllable
        };

        var created = await _deviceService.CreateDataPointAsync(dataPoint);

        if (device.IsEnabled)
        {
            await _collectionService.ReloadDeviceAsync(deviceId, HttpContext.RequestAborted);
            _logger.LogInformation("Created data point ID={DataPointId}, reloaded device ID={DeviceId}", created.Id, deviceId);
        }

        return CreatedAtAction(
            nameof(GetDataPoints),
            new { deviceId },
            ApiResponse<DataPointResponse>.Ok(MapDataPointToResponse(created, device.Name), "Data point created"));
    }

    [HttpPut("{deviceId:int}/datapoints/{dataPointId:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdateDataPoint(int deviceId, int dataPointId, [FromBody] UpdateDataPointRequest req)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
            return NotFound(ApiResponse.Fail($"Device ID={deviceId} was not found"));

        var dataPoint = await _deviceService.GetDataPointAsync(dataPointId);
        if (dataPoint == null || dataPoint.DeviceId != deviceId)
            return NotFound(ApiResponse.Fail($"Data point ID={dataPointId} was not found"));

        var configChanged = req.Address != null ||
                            req.DataType.HasValue ||
                            req.ModbusSlaveId.HasValue ||
                            req.ModbusFunctionCode.HasValue ||
                            req.ModbusByteOrder.HasValue ||
                            req.RegisterLength.HasValue ||
                            req.IsEnabled.HasValue;

        if (req.Name != null) dataPoint.Name = req.Name;
        if (req.Description != null) dataPoint.Description = req.Description;
        if (req.Address != null) dataPoint.Address = req.Address;
        if (req.DataType.HasValue) dataPoint.DataType = req.DataType.Value;
        if (req.Unit != null) dataPoint.Unit = req.Unit;
        if (req.ModbusSlaveId.HasValue) dataPoint.ModbusSlaveId = req.ModbusSlaveId.Value;
        if (req.ModbusFunctionCode.HasValue) dataPoint.ModbusFunctionCode = req.ModbusFunctionCode.Value;
        if (req.ModbusByteOrder.HasValue) dataPoint.ModbusByteOrder = req.ModbusByteOrder.Value;
        if (req.RegisterLength.HasValue) dataPoint.RegisterLength = req.RegisterLength.Value;
        if (req.IsEnabled.HasValue) dataPoint.IsEnabled = req.IsEnabled.Value;
        if (req.IsControllable.HasValue) dataPoint.IsControllable = req.IsControllable.Value;

        await _deviceService.UpdateDataPointAsync(dataPoint);

        if (configChanged && device.IsEnabled)
        {
            await _collectionService.ReloadDeviceAsync(deviceId, HttpContext.RequestAborted);
            _logger.LogInformation("Updated data point ID={DataPointId}, reloaded device ID={DeviceId}", dataPointId, deviceId);
        }

        return Ok(ApiResponse.Ok("Data point updated"));
    }

    [HttpDelete("{deviceId:int}/datapoints/{dataPointId:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> DeleteDataPoint(int deviceId, int dataPointId)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);

        await _deviceService.DeleteDataPointAsync(dataPointId);

        if (device != null && device.IsEnabled)
        {
            await _collectionService.ReloadDeviceAsync(deviceId, HttpContext.RequestAborted);
            _logger.LogInformation("Deleted data point ID={DataPointId}, reloaded device ID={DeviceId}", dataPointId, deviceId);
        }

        return Ok(ApiResponse.Ok("Data point deleted"));
    }

    [HttpPost("datapoints/control")]
    [ProducesResponseType(typeof(ApiResponse<DataPointRealtimeResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> ControlDataPointByTag([FromBody] ControlDataPointRequest req)
    {
        var controlValue = ConvertJsonValue(req.Value);
        var result = await _dataPointControlService.ControlByTagAsync(req.Tag, controlValue, HttpContext.RequestAborted);

        var response = new DataPointRealtimeResponse
        {
            DataPointId = result.DataPoint.Id,
            Tag = result.DataPoint.Tag,
            Value = result.Value,
            Quality = "Good",
            Timestamp = DateTime.UtcNow
        };

        return Ok(ApiResponse<DataPointRealtimeResponse>.Ok(response, "Data point control succeeded"));
    }

    private static DeviceListItem MapToListItem(Device d, int virtualPointCount = 0) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Code = d.Code,
        Description = d.Description,
        Protocol = d.Protocol.ToString(),
        ProtocolValue = (int)d.Protocol,
        Address = d.Address,
        Port = d.Port,
        PollingIntervalMs = d.PollingIntervalMs,
        IsEnabled = d.IsEnabled,
        DataPointCount = d.DataPoints.Count + virtualPointCount
    };

    private static DeviceResponse MapToDetail(Device d, int virtualPointCount = 0) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Code = d.Code,
        Description = d.Description,
        Protocol = d.Protocol.ToString(),
        ProtocolValue = (int)d.Protocol,
        Address = d.Address,
        Port = d.Port,
        IsEnabled = d.IsEnabled,
        PollingIntervalMs = d.PollingIntervalMs,
        DataPointCount = d.DataPoints.Count + virtualPointCount,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };

    private static DataPointResponse MapDataPointToResponse(DataPoint dp, string deviceName) => new()
    {
        Id = dp.Id,
        DeviceId = dp.DeviceId,
        DeviceName = deviceName,
        Name = dp.Name,
        Tag = dp.Tag,
        Description = dp.Description,
        Address = dp.Address,
        DataType = dp.DataType.ToString(),
        DataTypeValue = (int)dp.DataType,
        Unit = dp.Unit,
        IsEnabled = dp.IsEnabled,
        IsControllable = dp.IsControllable,
        CreatedAt = dp.CreatedAt,
        ModbusSlaveId = dp.ModbusSlaveId,
        ModbusFunctionCode = dp.ModbusFunctionCode,
        ModbusByteOrder = dp.ModbusByteOrder.HasValue ? (byte)dp.ModbusByteOrder.Value : (byte?)null,
        RegisterLength = dp.RegisterLength
    };

    private static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }
}
