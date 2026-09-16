using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Entities;

/// <summary>可复用的数据点模板。</summary>
public class DataPointTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CollectionProtocol Protocol { get; set; }
    public string PointsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>模板中的数据点配置，不包含设备 ID 和运行时数据。</summary>
public sealed class DataPointTemplateItem
{
    public string Name { get; set; } = string.Empty;
    public string TagSuffix { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public DataValueType DataType { get; set; }
    public string? Unit { get; set; }
    public byte? ModbusSlaveId { get; set; }
    public int? ModbusFunctionCode { get; set; }
    public ModbusByteOrder? ModbusByteOrder { get; set; }
    public byte RegisterLength { get; set; } = 1;
    public byte? ModbusBitIndex { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsControllable { get; set; }
}
