namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 数据转换类型枚举
/// </summary>
public enum TransformType
{
    /// <summary>
    /// 无转换
    /// </summary>
    None = 0,

    /// <summary>
    /// 线性变换：y = kx + b
    /// </summary>
    Linear = 1,

    /// <summary>
    /// 公式计算（支持表达式）
    /// </summary>
    Formula = 2,

    /// <summary>
    /// 查表转换
    /// </summary>
    LookupTable = 3,

    /// <summary>
    /// 单位换算
    /// </summary>
    UnitConversion = 4,

    /// <summary>
    /// 多项式变换：y = a*x^n + b*x^(n-1) + ... + c
    /// </summary>
    Polynomial = 5
}
