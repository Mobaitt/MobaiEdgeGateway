using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>
/// 创建规则请求 DTO
/// </summary>
public class CreateRuleRequest
{
    /// <summary>所属数据点 ID（为 null 时表示全局规则）</summary>
    public int? DataPointId { get; set; }

    /// <summary>所属设备 ID</summary>
    public int? DeviceId { get; set; }

    /// <summary>规则名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规则描述</summary>
    public string? Description { get; set; }

    /// <summary>规则类型</summary>
    public RuleType RuleType { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>规则优先级</summary>
    public int Priority { get; set; } = 100;

    /// <summary>规则配置（JSON 格式）</summary>
    public string RuleConfig { get; set; } = "{}";

    /// <summary>失败处理方式</summary>
    public FailureAction OnFailure { get; set; } = FailureAction.Pass;

    /// <summary>默认值</summary>
    public object? DefaultValue { get; set; }
}

/// <summary>
/// 更新规则请求 DTO
/// </summary>
public class UpdateRuleRequest
{
    /// <summary>规则 ID</summary>
    public int Id { get; set; }

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
}

/// <summary>
/// 测试规则请求 DTO
/// </summary>
public class TestRuleRequest
{
    /// <summary>规则配置</summary>
    public DataPointRule Rule { get; set; } = null!;

    /// <summary>测试数据</summary>
    public TestRuleData TestData { get; set; } = null!;
}

/// <summary>
/// 测试规则数据
/// </summary>
public class TestRuleData
{
    /// <summary>数据点 ID</summary>
    public int DataPointId { get; set; }

    /// <summary>设备 ID</summary>
    public int DeviceId { get; set; }

    /// <summary>Tag</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>测试值</summary>
    public object? Value { get; set; }
}
