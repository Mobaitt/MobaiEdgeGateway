using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EdgeGateway.Domain.Enums;

namespace EdgeGateway.WebApi.DTOs.Request;

public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ControlDataPointRequest
{
    [Required(ErrorMessage = "Tag is required")]
    [MaxLength(200)]
    public string Tag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Target value is required")]
    public JsonElement Value { get; set; }
}

public class QueryDataPointsRequest : PagedRequest
{
    public string? Search { get; set; }
    public DataValueType? DataType { get; set; }
    public bool? IsEnabled { get; set; }
}

public class QueryChannelMappingsRequest : PagedRequest
{
    public string? Search { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsVirtual { get; set; }
}

public class CreateDataPointRequest
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tag is required")]
    [MaxLength(200)]
    public string Tag { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Address is required")]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data type is required")]
    public DataValueType DataType { get; set; }

    [MaxLength(20)]
    public string? Unit { get; set; }

    public byte? ModbusSlaveId { get; set; } = 1;
    public byte? ModbusFunctionCode { get; set; } = 3;
    public ModbusByteOrder? ModbusByteOrder { get; set; }
    public byte RegisterLength { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public bool IsControllable { get; set; }
}

public class UpdateDataPointRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    public DataValueType? DataType { get; set; }

    [MaxLength(20)]
    public string? Unit { get; set; }

    public byte? ModbusSlaveId { get; set; }
    public byte? ModbusFunctionCode { get; set; }
    public ModbusByteOrder? ModbusByteOrder { get; set; }
    public byte? RegisterLength { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsControllable { get; set; }
}
