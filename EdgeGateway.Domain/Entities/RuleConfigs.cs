namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 限制规则配置（用于 RuleType.Limit）
/// </summary>
public class LimitRuleConfig
{
    /// <summary>
    /// 最大采集频率（毫秒），0 表示不限制
    /// </summary>
    public int MaxPollingRateMs { get; set; } = 0;

    /// <summary>
    /// 单次采集最大数据量，0 表示不限制
    /// </summary>
    public int MaxDataPointsPerRead { get; set; } = 0;

    /// <summary>
    /// 最大并发采集数，0 表示不限制
    /// </summary>
    public int MaxConcurrentReads { get; set; } = 0;

    /// <summary>
    /// 采集超时时间（毫秒），0 表示不限制
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 0;

    /// <summary>
    /// 数据最大值（超过此值将被限制）
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// 数据最小值（低于此值将被限制）
    /// </summary>
    public double? MinValue { get; set; }
}

/// <summary>
/// 转换规则配置（用于 RuleType.Transform）
/// </summary>
public class TransformRuleConfig
{
    /// <summary>
    /// 转换类型
    /// </summary>
    public Enums.TransformType TransformType { get; set; } = Enums.TransformType.None;

    /// <summary>
    /// 线性变换参数：y = kx + b 中的 k
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// 线性变换参数：y = kx + b 中的 b
    /// </summary>
    public double Offset { get; set; } = 0.0;

    /// <summary>
    /// 公式表达式（当 TransformType 为 Formula 时使用）
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>
    /// 多项式系数（从高次到低次，当 TransformType 为 Polynomial 时使用）
    /// 例如 [1, 2, 3] 表示 y = 1*x^2 + 2*x + 3
    /// </summary>
    public double[]? PolynomialCoefficients { get; set; }

    /// <summary>
    /// 查表数据（输入值 -> 输出值）
    /// </summary>
    public Dictionary<double, double>? LookupTable { get; set; }

    /// <summary>
    /// 源单位（当 TransformType 为 UnitConversion 时使用）
    /// </summary>
    public string? FromUnit { get; set; }

    /// <summary>
    /// 目标单位（当 TransformType 为 UnitConversion 时使用）
    /// </summary>
    public string? ToUnit { get; set; }
}

/// <summary>
/// 校验规则配置（用于 RuleType.Validation）
/// </summary>
public class ValidationRuleConfig
{
    /// <summary>
    /// 校验类型
    /// </summary>
    public Enums.ValidationType ValidationType { get; set; } = Enums.ValidationType.None;

    /// <summary>
    /// 最小值（用于 Range 校验）
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// 最大值（用于 Range 校验）
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// 最大变化率（单位：值/秒，用于 RateOfChange 校验）
    /// </summary>
    public double? MaxRateOfChange { get; set; }

    /// <summary>
    /// 死区值（用于 DeadBand 校验，变化小于此值不更新）
    /// </summary>
    public double? DeadBand { get; set; }

    /// <summary>
    /// 合理性校验表达式（用于 Rationality 校验）
    /// </summary>
    public string? RationalityExpression { get; set; }

    /// <summary>
    /// 固定值（用于 FixedValue 校验）
    /// </summary>
    public double? FixedValue { get; set; }

    /// <summary>
    /// 固定值容差（用于 FixedValue 校验）
    /// </summary>
    public double? FixedValueTolerance { get; set; } = 0.001;
}

/// <summary>
/// 计算规则配置（用于 RuleType.Calculation）
/// </summary>
public class CalculationRuleConfig
{
    /// <summary>
    /// 计算类型
    /// </summary>
    public Enums.CalculationType CalculationType { get; set; } = Enums.CalculationType.Custom;

    /// <summary>
    /// 自定义表达式（当 CalculationType 为 Custom 时使用）
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// 参与计算的数据点 Tags 列表
    /// </summary>
    public List<string> SourceDataPointTags { get; set; } = new();

    /// <summary>
    /// 权重列表（当 CalculationType 为 WeightedAverage 时使用）
    /// </summary>
    public List<double> Weights { get; set; } = new();

    /// <summary>
    /// 计算结果的数据类型
    /// </summary>
    public Enums.DataValueType ResultDataType { get; set; } = Enums.DataValueType.Float;

    /// <summary>
    /// 计算结果的小数位数
    /// </summary>
    public int DecimalPlaces { get; set; } = 2;
}
