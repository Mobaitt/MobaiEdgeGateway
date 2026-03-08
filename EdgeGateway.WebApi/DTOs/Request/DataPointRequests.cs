using EdgeGateway.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>分页查询请求基类</summary>
public class PagedRequest
{
    /// <summary>页码（从 1 开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页大小</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>分页查询数据点请求</summary>
public class QueryDataPointsRequest : PagedRequest
{
    /// <summary>搜索关键词（Tag/名称/地址）</summary>
    public string? Search { get; set; }

    /// <summary>数据类型过滤</summary>
    public DataValueType? DataType { get; set; }

    /// <summary>是否启用过滤</summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>分页查询通道映射请求</summary>
public class QueryChannelMappingsRequest : PagedRequest
{
    /// <summary>搜索关键词（Tag/名称）</summary>
    public string? Search { get; set; }

    /// <summary>是否启用过滤</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否虚拟数据点过滤</summary>
    public bool? IsVirtual { get; set; }
}

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

    /// <summary>Modbus 从站地址（仅 Modbus 协议使用，默认 1）</summary>
    public byte? ModbusSlaveId { get; set; } = 1;

    /// <summary>Modbus 功能码（01=线圈，02=离散输入，03=保持寄存器，04=输入寄存器）</summary>
    public byte? ModbusFunctionCode { get; set; } = 3;

    /// <summary>Modbus 字节顺序（仅 32 位数据类型使用）</summary>
    public ModbusByteOrder? ModbusByteOrder { get; set; }

    /// <summary>寄存器长度（1=16 位，2=32 位，4=64 位，默认 1）</summary>
    public byte RegisterLength { get; set; } = 1;

    /// <summary>是否启用采集（默认启用）</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>更新数据点请求</summary>
public class UpdateDataPointRequest
{
    /// <summary>数据点名称（如 "温度"）</summary>
    [MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>描述（可选）</summary>
    public string? Description { get; set; }

    /// <summary>寄存器地址或 OPC 节点 ID（如 "40001"）</summary>
    [MaxLength(200)]
    public string? Address { get; set; }

    /// <summary>数据类型</summary>
    public DataValueType? DataType { get; set; }

    /// <summary>工程量单位（可选，如 "℃"、"MPa"）</summary>
    [MaxLength(20)]
    public string? Unit { get; set; }

    /// <summary>Modbus 从站地址（仅 Modbus 协议使用）</summary>
    public byte? ModbusSlaveId { get; set; }

    /// <summary>Modbus 功能码</summary>
    public byte? ModbusFunctionCode { get; set; }

    /// <summary>Modbus 字节顺序</summary>
    public ModbusByteOrder? ModbusByteOrder { get; set; }

    /// <summary>寄存器长度（1=16 位，2=32 位，4=64 位）</summary>
    public byte? RegisterLength { get; set; }

    /// <summary>是否启用采集</summary>
    public bool? IsEnabled { get; set; }
}
