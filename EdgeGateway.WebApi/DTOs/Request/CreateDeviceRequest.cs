using EdgeGateway.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>创建设备请求</summary>
public class CreateDeviceRequest
{
    [Required(ErrorMessage = "设备名称不能为空")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "设备编码不能为空")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "通信协议不能为空")]
    public CollectionProtocol Protocol { get; set; }

    [Required(ErrorMessage = "设备地址不能为空")]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    public int? Port { get; set; }

    [Range(100, 60000, ErrorMessage = "采集周期范围：100ms - 60000ms")]
    public int PollingIntervalMs { get; set; } = 1000;

    public bool IsEnabled { get; set; } = true;
}
