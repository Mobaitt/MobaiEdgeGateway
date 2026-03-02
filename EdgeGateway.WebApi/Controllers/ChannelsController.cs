using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.WebApi.DTOs.Request;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// 发送通道管理接口
/// 提供通道的增删改查，以及数据点绑定操作
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChannelsController : ControllerBase
{
    private readonly DeviceManagementService _deviceService;
    private readonly DataSendService _sendService;
    private readonly ILogger<ChannelsController> _logger;

    public ChannelsController(DeviceManagementService deviceService, DataSendService sendService, ILogger<ChannelsController> logger)
    {
        _deviceService = deviceService;
        _sendService   = sendService;
        _logger        = logger;
    }

    /// <summary>获取所有发送通道</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ChannelResponse>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var channels = await _deviceService.GetAllChannelsAsync();
        var result   = channels.Select(MapToResponse).ToList();
        return Ok(ApiResponse<List<ChannelResponse>>.Ok(result, $"共 {result.Count} 个通道"));
    }

    /// <summary>新增发送通道</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ChannelResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> Create([FromBody] CreateChannelRequest req)
    {
        var channel = new Channel
        {
            Name        = req.Name,
            Code        = req.Code,
            Description = req.Description,
            Protocol    = req.Protocol,
            Endpoint    = req.Endpoint,
            // MQTT 配置
            MqttTopic = req.MqttTopic,
            MqttClientId = req.MqttClientId,
            MqttUsername = req.MqttUsername,
            MqttPassword = req.MqttPassword,
            MqttQos = req.MqttQos,
            // HTTP 配置
            HttpMethod = req.HttpMethod,
            HttpToken = req.HttpToken,
            HttpTimeout = req.HttpTimeout,
            HttpMode = req.HttpMode,
            // WebSocket 配置
            WsSubscribeTopic = req.WsSubscribeTopic,
            WsHeartbeatInterval = req.WsHeartbeatInterval,
            // 本地文件配置
            FileFormat = req.FileFormat,
            FilePath = req.FilePath,
            IsEnabled   = req.IsEnabled
        };

        var created = await _deviceService.CreateChannelAsync(channel);
        _logger.LogInformation("新增通道：{Name} (ID={Id})", created.Name, created.Id);

        return CreatedAtAction(nameof(GetAll), null,
            ApiResponse<ChannelResponse>.Ok(MapToResponse(created), "通道创建成功"));
    }

    /// <summary>
    /// 批量绑定数据点到通道
    /// 建立"数据点 → 通道"的发送映射关系
    /// </summary>
    /// <param name="channelId">目标通道 ID</param>
    /// <param name="req">包含数据点 ID 列表</param>
    [HttpPost("{channelId:int}/bind-datapoints")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> BindDataPoints(int channelId, [FromBody] BindDataPointsRequest req)
    {
        await _deviceService.BindDataPointsToChannelAsync(channelId, req.DataPointIds);
        await _sendService.RefreshChannelsCacheForceAsync();
        _logger.LogInformation("通道 ID={ChannelId} 绑定 {Count} 个数据点", channelId, req.DataPointIds.Count);
        return Ok(ApiResponse.Ok($"成功绑定 {req.DataPointIds.Count} 个数据点"));
    }

    /// <summary>
    /// 为通道添加单个数据点映射（支持设置别名）
    /// </summary>
    [HttpPost("{channelId:int}/mappings")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> AddMapping(int channelId, [FromBody] AddMappingRequest req)
    {
        await _deviceService.BindDataPointsToChannelAsync(channelId, [req.DataPointId]);
        return Ok(ApiResponse.Ok("映射关系添加成功"));
    }

    /// <summary>获取通道的所有数据点映射关系</summary>
    [HttpGet("{channelId:int}/mappings")]
    [ProducesResponseType(typeof(ApiResponse<List<MappingResponse>>), 200)]
    public async Task<IActionResult> GetMappings(int channelId)
    {
        var mappings = await _deviceService.GetChannelMappingsAsync(channelId);
        var result   = mappings.Select(m => new MappingResponse
        {
            Id              = m.Id,
            ChannelId       = m.ChannelId,
            ChannelName     = m.Channel?.Name ?? string.Empty,
            DataPointId     = m.DataPointId,
            DataPointTag    = m.DataPoint?.Tag ?? string.Empty,
            DataPointName   = m.DataPoint?.Name ?? string.Empty,
            AliasName       = m.AliasName,
            IsEnabled       = m.IsEnabled,
            CreatedAt       = m.CreatedAt
        }).ToList();

        return Ok(ApiResponse<List<MappingResponse>>.Ok(result, $"共 {result.Count} 条映射"));
    }

    /// <summary>删除通道的某个数据点映射</summary>
    [HttpDelete("mappings/{mappingId:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> DeleteMapping(int mappingId)
    {
        await _deviceService.DeleteMappingAsync(mappingId);
        await _sendService.RefreshChannelsCacheForceAsync();
        return Ok(ApiResponse.Ok("映射关系删除成功"));
    }

    /// <summary>更新通道配置</summary>
    [HttpPut("{channelId:int}")]
    [ProducesResponseType(typeof(ApiResponse<ChannelResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Update(int channelId, [FromBody] CreateChannelRequest req)
    {
        var channel = await _deviceService.GetAllChannelsAsync();
        var target = channel.FirstOrDefault(c => c.Id == channelId);

        if (target == null)
            return NotFound(ApiResponse.Fail($"通道 ID={channelId} 不存在"));

        target.Name = req.Name;
        target.Code = req.Code;
        target.Description = req.Description;
        target.Protocol = req.Protocol;
        target.Endpoint = req.Endpoint;
        // MQTT 配置
        target.MqttTopic = req.MqttTopic;
        target.MqttClientId = req.MqttClientId;
        target.MqttUsername = req.MqttUsername;
        target.MqttPassword = req.MqttPassword;
        target.MqttQos = req.MqttQos;
        // HTTP 配置
        target.HttpMethod = req.HttpMethod;
        target.HttpToken = req.HttpToken;
        target.HttpTimeout = req.HttpTimeout;
        target.HttpMode = req.HttpMode;
        // WebSocket 配置
        target.WsSubscribeTopic = req.WsSubscribeTopic;
        target.WsHeartbeatInterval = req.WsHeartbeatInterval;
        // 本地文件配置
        target.FileFormat = req.FileFormat;
        target.FilePath = req.FilePath;
        target.IsEnabled = req.IsEnabled;

        await _deviceService.UpdateChannelAsync(target);
        _logger.LogInformation("通道已更新：{Name} (ID={Id})", target.Name, channelId);

        return Ok(ApiResponse<ChannelResponse>.Ok(MapToResponse(target), "通道已更新"));
    }

    /// <summary>删除发送通道</summary>
    [HttpDelete("{channelId:int}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Delete(int channelId)
    {
        var channel = await _deviceService.GetAllChannelsAsync();
        var target = channel.FirstOrDefault(c => c.Id == channelId);

        if (target == null)
            return NotFound(ApiResponse.Fail($"通道 ID={channelId} 不存在"));

        await _deviceService.DeleteChannelAsync(channelId);
        _logger.LogInformation("通道已删除：{Name} (ID={Id})", target.Name, channelId);

        return Ok(ApiResponse.Ok("通道已删除"));
    }

    /// <summary>启用/停用发送通道</summary>
    [HttpPatch("{channelId:int}/toggle")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> Toggle(int channelId)
    {
        var channel = await _deviceService.GetAllChannelsAsync();
        var target = channel.FirstOrDefault(c => c.Id == channelId);

        if (target == null)
            return NotFound(ApiResponse.Fail($"通道 ID={channelId} 不存在"));

        if (target.IsEnabled)
        {
            // 当前是启用状态，执行停用操作
            await _deviceService.DisableChannelAsync(channelId);
            await _sendService.RefreshChannelsCacheForceAsync();
            _logger.LogInformation("通道已停用：{Name} (ID={Id})", target.Name, channelId);
            return Ok(ApiResponse.Ok("通道已停用，发送服务已断开"));
        }
        else
        {
            // 当前是停用状态，执行启用操作
            await _deviceService.EnableChannelAsync(channelId);
            await _sendService.RefreshChannelsCacheForceAsync();
            _logger.LogInformation("通道已启用：{Name} (ID={Id})", target.Name, channelId);
            return Ok(ApiResponse.Ok("通道已启用，发送服务已启动"));
        }
    }

    // ==================== 映射方法 ====================

    private static ChannelResponse MapToResponse(Channel c) => new()
    {
        Id                   = c.Id,
        Name                 = c.Name,
        Code                 = c.Code,
        Description          = c.Description,
        Protocol             = c.Protocol.ToString(),
        ProtocolValue        = (int)c.Protocol,
        Endpoint             = c.Endpoint,
        // MQTT 配置
        MqttTopic = c.MqttTopic,
        MqttClientId = c.MqttClientId,
        MqttUsername = c.MqttUsername,
        MqttPassword = c.MqttPassword,
        MqttQos = c.MqttQos,
        // HTTP 配置
        HttpMethod = c.HttpMethod,
        HttpToken = c.HttpToken,
        HttpTimeout = c.HttpTimeout,
        HttpMode = c.HttpMode,
        // WebSocket 配置
        WsSubscribeTopic = c.WsSubscribeTopic,
        WsHeartbeatInterval = c.WsHeartbeatInterval,
        // 本地文件配置
        FileFormat = c.FileFormat,
        FilePath = c.FilePath,
        IsEnabled            = c.IsEnabled,
        MappedDataPointCount = c.DataPointMappings.Count,
        CreatedAt            = c.CreatedAt
    };
}
