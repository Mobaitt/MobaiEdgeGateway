namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 计算类型枚举（用于虚拟节点和规则计算）
/// </summary>
public enum CalculationType
{
    /// <summary>
    /// 自定义表达式
    /// </summary>
    Custom = 0,

    /// <summary>
    /// 加法
    /// </summary>
    Sum = 1,

    /// <summary>
    /// 平均值
    /// </summary>
    Average = 2,

    /// <summary>
    /// 最大值
    /// </summary>
    Max = 3,

    /// <summary>
    /// 最小值
    /// </summary>
    Min = 4,

    /// <summary>
    /// 计数
    /// </summary>
    Count = 5,

    /// <summary>
    /// 标准差
    /// </summary>
    StandardDeviation = 6,

    /// <summary>
    /// 加权平均
    /// </summary>
    WeightedAverage = 7
}
