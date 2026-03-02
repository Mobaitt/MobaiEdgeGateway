namespace EdgeGateway.Domain.Enums;

/// <summary>
/// Modbus 寄存器字节顺序枚举
/// 用于多字节数据类型（如 Int32、Float）的字节交换
/// 
/// 说明：
/// - 一个 16 位寄存器包含 2 个字节：高字节 (A) 和低字节 (B)
/// - 32 位数据需要 2 个寄存器：寄存器 1(AB) 和寄存器 2(CD)
/// - 不同厂商设备可能采用不同的字节排列顺序
/// </summary>
public enum ModbusByteOrder
{
    /// <summary>
    /// ABCD - 大端模式 (Big Endian)
    /// 高字节在前，低字节在后
    /// 例如：0x12345678 → 寄存器 1:0x1234, 寄存器 2:0x5678
    /// </summary>
    ABCD = 1,

    /// <summary>
    /// BADC - 字节交换 (Byte Swap)
    /// 每个寄存器内字节交换
    /// 例如：0x12345678 → 寄存器 1:0x3412, 寄存器 2:0x7856
    /// </summary>
    BADC = 2,

    /// <summary>
    /// CDAB - 字交换 (Word Swap)
    /// 两个寄存器顺序交换
    /// 例如：0x12345678 → 寄存器 1:0x5678, 寄存器 2:0x1234
    /// </summary>
    CDAB = 3,

    /// <summary>
    /// DCBA - 完全交换 (Fully Swapped)
    /// 寄存器内字节交换 + 寄存器间交换
    /// 例如：0x12345678 → 寄存器 1:0x7856, 寄存器 2:0x3412
    /// </summary>
    DCBA = 4
}
