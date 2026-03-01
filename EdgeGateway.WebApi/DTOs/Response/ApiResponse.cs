namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>
/// API统一响应格式（泛型版，含数据）
/// 所有接口均返回此结构，前端通过 success 字段判断业务结果
/// </summary>
public class ApiResponse<T>
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>业务提示信息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>响应数据（失败时为 null）</summary>
    public T? Data { get; set; }

    /// <summary>响应时间戳（Unix毫秒）</summary>
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static ApiResponse<T> Ok(T data, string message = "操作成功") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message };
}
