using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.WebApi.DTOs.Request;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// 虚拟节点管理控制器
/// 虚拟节点依附于普通设备，可以像普通数据点一样被管理和发送
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VirtualNodesController : ControllerBase
{
    private readonly VirtualNodeManagementService _virtualNodeService;
    private readonly ILogger<VirtualNodesController> _logger;

    public VirtualNodesController(
        VirtualNodeManagementService virtualNodeService,
        ILogger<VirtualNodesController> logger)
    {
        _virtualNodeService = virtualNodeService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有虚拟数据点
    /// </summary>
    [HttpGet("points")]
    [ProducesResponseType(typeof(ApiResponse<List<VirtualDataPointResponse>>), 200)]
    public async Task<IActionResult> GetAllVirtualDataPoints()
    {
        var points = await _virtualNodeService.GetAllVirtualDataPointsAsync();
        var result = points.Select(VirtualDataPointResponse.FromEntity).ToList();
        return Ok(ApiResponse<List<VirtualDataPointResponse>>.Ok(result, $"共 {result.Count} 个虚拟数据点"));
    }

    /// <summary>
    /// 获取指定设备下的所有虚拟数据点
    /// </summary>
    [HttpGet("devices/{deviceId}/points")]
    [ProducesResponseType(typeof(ApiResponse<List<VirtualDataPointResponse>>), 200)]
    public async Task<IActionResult> GetVirtualDataPointsByDevice(int deviceId)
    {
        var points = await _virtualNodeService.GetVirtualDataPointsByDeviceIdAsync(deviceId);
        var result = points.Select(VirtualDataPointResponse.FromEntity).ToList();
        return Ok(ApiResponse<List<VirtualDataPointResponse>>.Ok(result, $"设备 {deviceId} 共 {result.Count} 个虚拟数据点"));
    }

    /// <summary>
    /// 根据 ID 获取虚拟数据点
    /// </summary>
    [HttpGet("points/{id}")]
    [ProducesResponseType(typeof(ApiResponse<VirtualDataPointResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetVirtualDataPointById(int id)
    {
        var point = await _virtualNodeService.GetVirtualDataPointByIdAsync(id);
        if (point == null)
            return NotFound(ApiResponse.Fail($"虚拟数据点 ID={id} 不存在"));

        return Ok(ApiResponse<VirtualDataPointResponse>.Ok(VirtualDataPointResponse.FromEntity(point)));
    }

    /// <summary>
    /// 创建虚拟数据点
    /// </summary>
    [HttpPost("points")]
    [ProducesResponseType(typeof(ApiResponse<VirtualDataPointResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> CreateVirtualDataPoint([FromBody] CreateVirtualDataPointRequest request)
    {
        try
        {
            var point = new VirtualDataPoint
            {
                DeviceId = request.DeviceId,
                Name = request.Name,
                Tag = request.Tag,
                Description = request.Description,
                Expression = request.Expression,
                CalculationType = request.CalculationType,
                DataType = request.DataType,
                Unit = request.Unit,
                IsEnabled = request.IsEnabled
            };

            var createdPoint = await _virtualNodeService.CreateVirtualDataPointAsync(point);
            _logger.LogInformation("虚拟数据点创建成功：{PointId}", createdPoint.Id);

            return CreatedAtAction(nameof(GetVirtualDataPointById), new { id = createdPoint.Id },
                ApiResponse<VirtualDataPointResponse>.Ok(VirtualDataPointResponse.FromEntity(createdPoint), "虚拟数据点创建成功"));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟数据点创建失败");
            return StatusCode(500, ApiResponse.Fail($"虚拟数据点创建失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 更新虚拟数据点
    /// </summary>
    [HttpPut("points/{id}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdateVirtualDataPoint(int id, [FromBody] UpdateVirtualDataPointRequest request)
    {
        if (id != request.Id)
            return BadRequest(ApiResponse.Fail("ID 不匹配"));

        try
        {
            var point = new VirtualDataPoint
            {
                Id = request.Id,
                DeviceId = request.DeviceId,
                Name = request.Name,
                Tag = request.Tag,
                Description = request.Description,
                Expression = request.Expression,
                CalculationType = request.CalculationType,
                DataType = request.DataType,
                Unit = request.Unit,
                IsEnabled = request.IsEnabled
            };

            await _virtualNodeService.UpdateVirtualDataPointAsync(point);
            return Ok(ApiResponse.Ok("虚拟数据点更新成功"));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟数据点更新失败");
            return StatusCode(500, ApiResponse.Fail($"虚拟数据点更新失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 删除虚拟数据点
    /// </summary>
    [HttpDelete("points/{id}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> DeleteVirtualDataPoint(int id)
    {
        try
        {
            await _virtualNodeService.DeleteVirtualDataPointAsync(id);
            return Ok(ApiResponse.Ok("虚拟数据点删除成功"));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟数据点删除失败");
            return StatusCode(500, ApiResponse.Fail($"虚拟数据点删除失败：{ex.Message}"));
        }
    }
}
