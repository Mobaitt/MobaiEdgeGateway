using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>
/// 更新设备请求。
/// </summary>
public class UpdateDeviceRequest
{
    [Required(ErrorMessage = "设备名称不能为空")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "设备地址不能为空")]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    public int? Port { get; set; }

    [Range(100, 60000, ErrorMessage = "采集周期范围：100ms - 60000ms")]
    public int PollingIntervalMs { get; set; } = 1000;

    public bool IsEnabled { get; set; } = true;
    public bool ReconnectEnabled { get; set; } = true;

    [Range(1, 100, ErrorMessage = "单轮重连次数范围：1 - 100")]
    public int ReconnectRetryCount { get; set; } = 3;

    [Range(100, 60000, ErrorMessage = "单次重连间隔范围：100ms - 60000ms")]
    public int ReconnectRetryDelayMs { get; set; } = 1000;

    [Range(500, 600000, ErrorMessage = "轮次重连间隔范围：500ms - 600000ms")]
    public int ReconnectIntervalMs { get; set; } = 5000;

    [Range(1, 100, ErrorMessage = "连续读取失败阈值范围：1 - 100")]
    public int MaxConsecutiveReadFailures { get; set; } = 3;

    [Range(3, 1000, ErrorMessage = "失败比例统计窗口范围：3 - 1000")]
    public int ReadFailureWindowSize { get; set; } = 10;

    [Range(1, 100, ErrorMessage = "读取失败比例阈值范围：1 - 100")]
    public double ReadFailureRateThresholdPercent { get; set; } = 50;
}
