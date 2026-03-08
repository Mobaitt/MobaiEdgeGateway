using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;

namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>
/// 虚拟数据点响应 DTO
/// </summary>
public class VirtualDataPointResponse
{
    /// <summary>虚拟数据点 ID</summary>
    public int Id { get; set; }

    /// <summary>所属设备 ID</summary>
    public int DeviceId { get; set; }

    /// <summary>所属设备名称</summary>
    public string? DeviceName { get; set; }

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

    /// <summary>依赖的 Tags</summary>
    public List<string> DependencyTags { get; set; } = new();

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }

    public static VirtualDataPointResponse FromEntity(VirtualDataPoint point)
    {
        var dependencyTags = new List<string>();
        if (!string.IsNullOrEmpty(point.DependencyTags))
        {
            try
            {
                dependencyTags = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(point.DependencyTags) ?? new List<string>();
            }
            catch
            {
                // 解析失败时返回空列表
            }
        }

        return new VirtualDataPointResponse
        {
            Id = point.Id,
            DeviceId = point.DeviceId,
            DeviceName = point.Device?.Name,
            Name = point.Name,
            Tag = point.Tag,
            Description = point.Description,
            Expression = point.Expression,
            CalculationType = point.CalculationType,
            DataType = point.DataType,
            Unit = point.Unit,
            IsEnabled = point.IsEnabled,
            DependencyTags = dependencyTags,
            CreatedAt = point.CreatedAt
        };
    }
}
