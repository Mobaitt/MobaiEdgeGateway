namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 发送操作结果
/// 由具体发送策略执行发送后返回，包含成功与否及错误信息
/// </summary>
public class SendResult
{
    /// <summary>是否发送成功</summary>
    public bool IsSuccess { get; set; }

    /// <summary>错误信息（仅失败时有值）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>实际成功发送的数据条数</summary>
    public int SentCount { get; set; }

    /// <summary>发送时间（UTC）</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>构建成功结果</summary>
    public static SendResult Success(int count) =>
        new() { IsSuccess = true, SentCount = count };

    /// <summary>构建失败结果</summary>
    public static SendResult Failure(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };
}
