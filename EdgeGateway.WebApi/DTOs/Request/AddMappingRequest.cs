using System.ComponentModel.DataAnnotations;

namespace EdgeGateway.WebApi.DTOs.Request;

/// <summary>添加单条数据点映射请求（支持设置别名）</summary>
public class AddMappingRequest
{
    /// <summary>目标数据点ID</summary>
    [Required(ErrorMessage = "数据点ID不能为空")]
    public int DataPointId { get; set; }

    /// <summary>
    /// 发送时的字段别名（可选）
    /// 不填则使用数据点的 Tag 作为字段名
    /// 示例：将 "Device01.Temperature" 映射为 "temperature"
    /// </summary>
    [MaxLength(100)]
    public string? AliasName { get; set; }
}
