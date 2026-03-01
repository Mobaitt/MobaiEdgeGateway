namespace EdgeGateway.Domain.Enums;

/// <summary>
/// 采集协议枚举 - 决定使用哪种采集策略
/// </summary>
public enum CollectionProtocol
{
    /// <summary>Modbus TCP / Modbus RTU</summary>
    Modbus = 1,

    /// <summary>OPC UA</summary>
    OpcUa = 2,

    /// <summary>西门子S7通信协议</summary>
    S7 = 3,

    /// <summary>自定义HTTP采集（主动拉取）</summary>
    Http = 4,

    /// <summary>模拟数据（用于测试）</summary>
    Simulator = 99
}
