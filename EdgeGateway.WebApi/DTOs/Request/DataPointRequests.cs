using EdgeGateway.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>创建数据点请求</summary>
public class CreateDataPointRequest
{
    /// <summary>数据点名称（如 "温度"）</summary>
    [Required(ErrorMessage = "数据点名称不能为空")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Tag（全局唯一标识，如 "Device01.Temperature"）</summary>
    [Required(ErrorMessage = "Tag不能为空")]
    [MaxLength(200)]
    public string Tag { get; set; } = string.Empty;

    /// <summary>描述（可选）</summary>
    public string? Description { get; set; }

    /// <summary>寄存器地址或OPC节点ID（如 "40001"）</summary>
    [Required(ErrorMessage = "数据地址不能为空")]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    /// <summary>数据类型（1=Bool 2=Int16 3=Int32 4=Int64 5=Float 6=Double 7=String）</summary>
    [Required(ErrorMessage = "数据类型不能为空")]
    public DataValueType DataType { get; set; }

    /// <summary>工程量单位（可选，如 "℃"、"MPa"）</summary>
    [MaxLength(20)]
    public string? Unit { get; set; }

    /// <summary>是否启用采集（默认启用）</summary>
    public bool IsEnabled { get; set; } = true;
}
