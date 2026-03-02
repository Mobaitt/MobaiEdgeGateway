namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 规则类型枚举
/// </summary>
public enum RuleType
{
    /// <summary>
    /// 限制规则（采集频率、数据量、并发数等）
    /// </summary>
    Limit = 0,

    /// <summary>
    /// 转换规则（线性变换、公式计算、查表转换）
    /// </summary>
    Transform = 1,

    /// <summary>
    /// 校验规则（阈值检查、变化率、死区、合理性）
    /// </summary>
    Validation = 2,

    /// <summary>
    /// 计算规则（多数据点组合计算）
    /// </summary>
    Calculation = 3
}
