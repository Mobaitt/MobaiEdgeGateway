namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 数据点值类型枚举 - 标识采集到的原始值的数据类型
/// </summary>
public enum DataValueType
{
    Bool   = 1,
    Int16  = 2,
    UInt16 = 3,
    Int32  = 4,
    UInt32 = 5,
    Float  = 6,
    Int64  = 7,
    UInt64 = 8,
    Double = 9,
    String = 10,
    /// <summary>16 位无符号寄存器，以十六进制字符串展示。</summary>
    Hex = 11,
    /// <summary>16 位无符号寄存器，以二进制字符串展示。</summary>
    Binary = 12
}
