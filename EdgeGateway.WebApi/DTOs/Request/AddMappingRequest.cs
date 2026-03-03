using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>添加单条数据点映射请求</summary>
public class AddMappingRequest
{
    /// <summary>目标数据点 ID</summary>
    [Required(ErrorMessage = "数据点 ID 不能为空")]
    public int DataPointId { get; set; }
}
