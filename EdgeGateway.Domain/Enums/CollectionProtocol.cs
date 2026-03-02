namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 采集协议枚举 - 添加虚拟设备类型
/// </summary>
public enum CollectionProtocol
{
    /// <summary>
    /// 模拟采集（随机数据）
    /// </summary>
    Simulator = 0,

    /// <summary>
    /// Modbus TCP/RTU
    /// </summary>
    Modbus = 1,

    /// <summary>
    /// OPC UA
    /// </summary>
    OpcUa = 2,

    /// <summary>
    /// 虚拟设备（通过计算得出）
    /// </summary>
    Virtual = 3,

    /// <summary>
    /// S7 PLC（西门子）
    /// </summary>
    S7 = 4
}
