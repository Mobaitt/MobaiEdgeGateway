namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 数据校验类型枚举
/// </summary>
public enum ValidationType
{
    /// <summary>
    /// 无校验
    /// </summary>
    None = 0,

    /// <summary>
    /// 阈值校验（最小值、最大值）
    /// </summary>
    Range = 1,

    /// <summary>
    /// 变化率校验（单位时间内最大变化量）
    /// </summary>
    RateOfChange = 2,

    /// <summary>
    /// 死区校验（超过死区才更新）
    /// </summary>
    DeadBand = 3,

    /// <summary>
    /// 合理性校验（自定义规则）
    /// </summary>
    Rationality = 4,

    /// <summary>
    /// 固定值校验（只能等于指定值）
    /// </summary>
    FixedValue = 5
}
