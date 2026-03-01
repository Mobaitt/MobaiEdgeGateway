namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 采集数据质量枚举
/// 参考OPC UA标准质量码定义
/// </summary>
public enum DataQuality
{
    /// <summary>好质量，采集成功且数据可信</summary>
    Good = 0,

    /// <summary>差质量，采集失败或设备离线</summary>
    Bad = 1,

    /// <summary>不确定质量，数据可能不准确</summary>
    Uncertain = 2
}
