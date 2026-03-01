using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>批量绑定数据点到通道请求</summary>
public class BindDataPointsRequest
{
    /// <summary>要绑定到该通道的数据点ID列表（至少一个）</summary>
    [Required]
    [MinLength(1, ErrorMessage = "至少选择一个数据点")]
    public List<int> DataPointIds { get; set; } = new();
}
