using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// 网关状态与运行管理接口
/// 提供网关运行状态查询、设备采集任务的动态启停控制
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GatewayController : ControllerBase
{
    private readonly DataCollectionService _collectionService;
    private readonly DataSendService _sendService;
    private readonly IDeviceRepository _deviceRepo;
    private readonly IChannelRepository _channelRepo;
    private readonly ILogger<GatewayController> _logger;

    public GatewayController(
        DataCollectionService collectionService,
        DataSendService sendService,
        IDeviceRepository deviceRepo,
        IChannelRepository channelRepo,
        ILogger<GatewayController> logger)
    {
        _collectionService = collectionService;
        _sendService       = sendService;
        _deviceRepo        = deviceRepo;
        _channelRepo       = channelRepo;
        _logger            = logger;
    }

    /// <summary>
    /// 获取网关整体运行状态
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<GatewayStatusResponse>), 200)]
    public async Task<IActionResult> GetStatus()
    {
        var allDevices  = (await _deviceRepo.GetAllAsync()).ToList();
        var allChannels = (await _channelRepo.GetAllAsync()).ToList();

        var status = new GatewayStatusResponse
        {
            IsRunning       = true,  // 能调到此接口说明服务正在运行
            TotalDevices    = allDevices.Count,
            EnabledDevices  = allDevices.Count(d => d.IsEnabled),
            TotalChannels   = allChannels.Count,
            EnabledChannels = allChannels.Count(c => c.IsEnabled),
            TotalDataPoints = allDevices.Sum(d => d.DataPoints.Count),
            ServerTime      = DateTime.UtcNow
        };

        return Ok(ApiResponse<GatewayStatusResponse>.Ok(status));
    }

    /// <summary>
    /// 停止指定设备的采集任务（热停止，不需要重启服务）
    /// </summary>
    /// <param name="deviceId">设备ID</param>
    [HttpPost("devices/{deviceId:int}/stop")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public IActionResult StopDeviceCollection(int deviceId)
    {
        _collectionService.StopDevice(deviceId);
        _logger.LogInformation("通过API停止设备 ID={DeviceId} 的采集任务", deviceId);
        return Ok(ApiResponse.Ok($"设备 ID={deviceId} 采集任务已停止"));
    }

    /// <summary>
    /// 健康检查接口（供负载均衡/监控系统使用）
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public IActionResult HealthCheck() =>
        Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
