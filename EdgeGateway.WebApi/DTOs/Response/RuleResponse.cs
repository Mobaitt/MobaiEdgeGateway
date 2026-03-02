using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;

namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>
/// 规则响应 DTO
/// </summary>
public class RuleResponse
{
    /// <summary>规则 ID</summary>
    public int Id { get; set; }

    /// <summary>所属数据点 ID</summary>
    public int? DataPointId { get; set; }

    /// <summary>数据点名称</summary>
    public string? DataPointName { get; set; }

    /// <summary>所属设备 ID</summary>
    public int? DeviceId { get; set; }

    /// <summary>设备名称</summary>
    public string? DeviceName { get; set; }

    /// <summary>规则名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规则描述</summary>
    public string? Description { get; set; }

    /// <summary>规则类型</summary>
    public RuleType RuleType { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>规则优先级</summary>
    public int Priority { get; set; }

    /// <summary>规则配置（JSON 格式）</summary>
    public string RuleConfig { get; set; } = "{}";

    /// <summary>失败处理方式</summary>
    public FailureAction OnFailure { get; set; }

    /// <summary>默认值</summary>
    public object? DefaultValue { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; }

    public static RuleResponse FromEntity(DataPointRule rule)
    {
        return new RuleResponse
        {
            Id = rule.Id,
            DataPointId = rule.DataPointId,
            DataPointName = rule.DataPoint?.Name,
            DeviceId = rule.DeviceId,
            DeviceName = rule.Device?.Name,
            Name = rule.Name,
            Description = rule.Description,
            RuleType = rule.RuleType,
            IsEnabled = rule.IsEnabled,
            Priority = rule.Priority,
            RuleConfig = rule.RuleConfig,
            OnFailure = rule.OnFailure,
            DefaultValue = rule.DefaultValue,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }
}

/// <summary>
/// 规则执行结果响应
/// </summary>
public class RuleExecutionResultResponse
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>处理后的值</summary>
    public object? Value { get; set; }

    /// <summary>数据质量</summary>
    public DataQuality Quality { get; set; }

    /// <summary>错误消息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>触发的规则列表</summary>
    public List<string> TriggeredRules { get; set; } = new();

    /// <summary>是否拒绝</summary>
    public bool ShouldReject { get; set; }
}
