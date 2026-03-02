namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>数据点详情响应</summary>
public class DataPointResponse
{
    public int Id { get; set; }
    public int DeviceId { get; set; }

    /// <summary>所属设备名称（冗余字段，方便前端展示）</summary>
    public string DeviceName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>全局唯一 Tag 标识，如 "Device01.Temperature"</summary>
    public string Tag { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;

    /// <summary>数据类型名称（字符串，如 "Float"）</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>数据类型枚举值（数字）</summary>
    public int DataTypeValue { get; set; }

    /// <summary>工程量单位（如 "℃"、"MPa"）</summary>
    public string? Unit { get; set; }

    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }

    // Modbus 配置字段
    public byte? ModbusSlaveId { get; set; }
    public int? ModbusFunctionCode { get; set; }
    public byte? ModbusByteOrder { get; set; }
    public byte RegisterLength { get; set; }
}

/// <summary>数据点实时数据响应</summary>
public class DataPointRealtimeResponse
{
    public int DataPointId { get; set; }
    public object? Value { get; set; }
    public string Quality { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
