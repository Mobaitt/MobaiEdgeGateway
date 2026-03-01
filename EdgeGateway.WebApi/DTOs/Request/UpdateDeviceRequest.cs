using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>更新设备请求（编码和协议不允许修改）</summary>
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
}
