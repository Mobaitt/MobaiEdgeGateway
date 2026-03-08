using EdgeGateway.Domain.Options;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EdgeGateway.WebApi.Controllers;

/// <summary>
/// 系统管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly DemoModeOptions _demoModeOptions;
    private readonly ILogger<SystemController> _logger;
    private readonly IOptionsSnapshot<DemoModeOptions> _optionsSnapshot;

    public SystemController(
        IOptions<DemoModeOptions> demoModeOptions,
        IOptionsSnapshot<DemoModeOptions> optionsSnapshot,
        ILogger<SystemController> logger)
    {
        _demoModeOptions = demoModeOptions.Value;
        _optionsSnapshot = optionsSnapshot;
        _logger = logger;
    }

    /// <summary>
    /// 获取演示模式状态
    /// </summary>
    [HttpGet("demo-mode")]
    [ProducesResponseType(typeof(ApiResponse<DemoModeStatusResponse>), 200)]
    public IActionResult GetDemoModeStatus()
    {
        var options = _optionsSnapshot.Value;
        var response = new DemoModeStatusResponse
        {
            Enabled = options.Enabled,
            Message = options.Message
        };

        return Ok(ApiResponse<DemoModeStatusResponse>.Ok(response, $"演示模式已{ (options.Enabled ? "启用" : "禁用") }"));
    }
}

/// <summary>
/// 演示模式状态响应
/// </summary>
public class DemoModeStatusResponse
{
    /// <summary>是否启用演示模式</summary>
    public bool Enabled { get; set; }

    /// <summary>提示消息</summary>
    public string Message { get; set; } = string.Empty;
}
