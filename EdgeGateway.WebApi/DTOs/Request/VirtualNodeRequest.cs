using EdgeGateway.Domain.Enums;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>
/// 创建虚拟数据点请求 DTO
/// </summary>
public class CreateVirtualDataPointRequest
{
    /// <summary>所属设备 ID</summary>
    public int DeviceId { get; set; }

    /// <summary>虚拟数据点名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>虚拟数据点标签</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>虚拟数据点描述</summary>
    public string? Description { get; set; }

    /// <summary>计算表达式</summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>计算类型</summary>
    public CalculationType CalculationType { get; set; } = CalculationType.Custom;

    /// <summary>数据类型</summary>
    public DataValueType DataType { get; set; } = DataValueType.Float;

    /// <summary>单位</summary>
    public string? Unit { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 更新虚拟数据点请求 DTO
/// </summary>
public class UpdateVirtualDataPointRequest
{
    /// <summary>虚拟数据点 ID</summary>
    public int Id { get; set; }

    /// <summary>所属设备 ID</summary>
    public int DeviceId { get; set; }

    /// <summary>虚拟数据点名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>虚拟数据点标签</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>虚拟数据点描述</summary>
    public string? Description { get; set; }

    /// <summary>计算表达式</summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>计算类型</summary>
    public CalculationType CalculationType { get; set; }

    /// <summary>数据类型</summary>
    public DataValueType DataType { get; set; }

    /// <summary>单位</summary>
    public string? Unit { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }
}
