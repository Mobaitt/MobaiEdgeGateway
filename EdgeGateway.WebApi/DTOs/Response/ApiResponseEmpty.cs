namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>
/// API统一响应格式（无数据版）
/// 用于只需告知成功/失败而无返回数据的操作（如删除、更新、启停）
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "操作成功") =>
        new() { Success = true, Message = message };

    public new static ApiResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
