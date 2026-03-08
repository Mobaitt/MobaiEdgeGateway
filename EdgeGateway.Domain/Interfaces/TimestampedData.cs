namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 带时间戳的数据包装（支持缓存过期机制）
/// </summary>
public class TimestampedData
{
    public CollectedData Data { get; set; } = null!;
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 检查数据是否已过期（超过 30 秒没有更新）
    /// </summary>
    public bool IsExpired(TimeSpan expiration)
    {
        return LastUpdateTime == DateTime.MinValue ||
               (DateTime.UtcNow - LastUpdateTime) > expiration;
    }
}