using System.ComponentModel.DataAnnotations;
using EdgeGateway.Domain.Enums;

namespace EdgeGateway.WebApi.DTOs.Request;

public class CreateDataPointTemplateRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class ApplyDataPointTemplateRequest
{
    public bool OverwriteExisting { get; set; }
}

public class DataPointTemplatePointRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string TagSuffix { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required, MaxLength(200)]
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

public class UpdateDataPointTemplateRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    public List<DataPointTemplatePointRequest> Points { get; set; } = new();
}
