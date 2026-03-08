using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.WebApi.DTOs.Request;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// 规则管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RulesController : ControllerBase
{
    private readonly RuleManagementService _ruleService;
    private readonly ILogger<RulesController> _logger;

    public RulesController(
        RuleManagementService ruleService,
        ILogger<RulesController> logger)
    {
        _ruleService = ruleService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有规则
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<RuleResponse>>), 200)]
    public async Task<IActionResult> GetAllRules()
    {
        var rules = await _ruleService.GetAllRulesAsync();
        var result = rules.Select(RuleResponse.FromEntity).ToList();
        return Ok(ApiResponse<List<RuleResponse>>.Ok(result, $"共 {result.Count} 条规则"));
    }

    /// <summary>
    /// 根据 ID 获取规则
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<RuleResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetRuleById(int id)
    {
        var rule = await _ruleService.GetRuleByIdAsync(id);
        if (rule == null)
            return NotFound(ApiResponse.Fail($"规则 ID={id} 不存在"));

        return Ok(ApiResponse<RuleResponse>.Ok(RuleResponse.FromEntity(rule)));
    }

    /// <summary>
    /// 获取指定数据点的所有规则
    /// </summary>
    [HttpGet("datapoint/{dataPointId}")]
    [ProducesResponseType(typeof(ApiResponse<List<RuleResponse>>), 200)]
    public async Task<IActionResult> GetRulesByDataPoint(int dataPointId)
    {
        var rules = await _ruleService.GetRulesByDataPointIdAsync(dataPointId);
        var result = rules.Select(RuleResponse.FromEntity).ToList();
        return Ok(ApiResponse<List<RuleResponse>>.Ok(result, $"共 {result.Count} 条规则"));
    }

    /// <summary>
    /// 获取指定设备的所有规则
    /// </summary>
    [HttpGet("device/{deviceId}")]
    [ProducesResponseType(typeof(ApiResponse<List<RuleResponse>>), 200)]
    public async Task<IActionResult> GetRulesByDevice(int deviceId)
    {
        var rules = await _ruleService.GetRulesByDeviceIdAsync(deviceId);
        var result = rules.Select(RuleResponse.FromEntity).ToList();
        return Ok(ApiResponse<List<RuleResponse>>.Ok(result, $"共 {result.Count} 条规则"));
    }

    /// <summary>
    /// 获取全局规则
    /// </summary>
    [HttpGet("global")]
    [ProducesResponseType(typeof(ApiResponse<List<RuleResponse>>), 200)]
    public async Task<IActionResult> GetGlobalRules()
    {
        var rules = await _ruleService.GetGlobalRulesAsync();
        var result = rules.Select(RuleResponse.FromEntity).ToList();
        return Ok(ApiResponse<List<RuleResponse>>.Ok(result, $"共 {result.Count} 条全局规则"));
    }

    /// <summary>
    /// 创建规则
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RuleResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest request)
    {
        try
        {
            var rule = new DataPointRule
            {
                DataPointIds = request.DataPointIds ?? new List<int>(),
                DeviceId = request.DeviceId,
                Name = request.Name,
                Description = request.Description,
                RuleType = request.RuleType,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                RuleConfig = request.RuleConfig,
                OnFailure = request.OnFailure,
                DefaultValueJson = request.DefaultValue != null ? System.Text.Json.JsonSerializer.Serialize(request.DefaultValue) : null
            };

            var createdRule = await _ruleService.CreateRuleAsync(rule);
            _logger.LogInformation("规则创建成功：{RuleId}", createdRule.Id);

            return CreatedAtAction(nameof(GetRuleById), new { id = createdRule.Id },
                ApiResponse<RuleResponse>.Ok(RuleResponse.FromEntity(createdRule), "规则创建成功"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则创建失败");
            return StatusCode(500, ApiResponse.Fail($"规则创建失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 更新规则
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] UpdateRuleRequest request)
    {
        if (id != request.Id)
            return BadRequest(ApiResponse.Fail("ID 不匹配"));

        try
        {
            var rule = new DataPointRule
            {
                Id = request.Id,
                DataPointIds = request.DataPointIds ?? new List<int>(),
                DeviceId = request.DeviceId,
                Name = request.Name,
                Description = request.Description,
                RuleType = request.RuleType,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                RuleConfig = request.RuleConfig,
                OnFailure = request.OnFailure,
                DefaultValueJson = request.DefaultValue != null ? System.Text.Json.JsonSerializer.Serialize(request.DefaultValue) : null,
                UpdatedAt = DateTime.UtcNow
            };

            var updatedRule = await _ruleService.UpdateRuleAsync(rule);
            return Ok(ApiResponse.Ok("规则更新成功"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则更新失败");
            return StatusCode(500, ApiResponse.Fail($"规则更新失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 删除规则
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> DeleteRule(int id)
    {
        try
        {
            await _ruleService.DeleteRuleAsync(id);
            return Ok(ApiResponse.Ok("规则删除成功"));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则删除失败");
            return StatusCode(500, ApiResponse.Fail($"规则删除失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 启用/禁用规则
    /// </summary>
    [HttpPatch("{id}/toggle")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> ToggleRule(int id, [FromBody] bool isEnabled)
    {
        try
        {
            await _ruleService.ToggleRuleAsync(id, isEnabled);
            return Ok(ApiResponse.Ok(isEnabled ? "规则已启用" : "规则已禁用"));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则切换失败");
            return StatusCode(500, ApiResponse.Fail($"规则切换失败：{ex.Message}"));
        }
    }
}
