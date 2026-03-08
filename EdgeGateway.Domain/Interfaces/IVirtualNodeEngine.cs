using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 虚拟节点计算结果
/// </summary>
public class VirtualNodeCalculationResult
{
    /// <summary>
    /// 设备ID
    /// </summary>
    public int DeviceId { get; set; }

    /// <summary>
    /// 是否计算成功
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// 计算结果值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 数据质量
    /// </summary>
    public DataQuality Quality { get; set; } = DataQuality.Good;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 计算时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 虚拟数据点 ID
    /// </summary>
    public int VirtualDataPointId { get; set; }

    /// <summary>
    /// 虚拟数据点 Tag
    /// </summary>
    public string? VirtualDataPointTag { get; set; }

    /// <summary>
    /// 依赖的数据点值（用于调试）
    /// </summary>
    public Dictionary<string, object?> DependencyValues { get; set; } = new();

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static VirtualNodeCalculationResult Ok(object? value, Dictionary<string, object?>? dependencyValues = null)
    {
        return new VirtualNodeCalculationResult
        {
            Success = true,
            Value = value,
            DependencyValues = dependencyValues ?? new Dictionary<string, object?>()
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static VirtualNodeCalculationResult Fail(string errorMessage)
    {
        return new VirtualNodeCalculationResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Quality = DataQuality.Bad
        };
    }
}

/// <summary>
/// 虚拟节点计算引擎接口
/// </summary>
public interface IVirtualNodeEngine
{
    /// <summary>
    /// 刷新缓存
    /// </summary>
    Task RefreshCacheAsync();

    /// <summary>
    /// 计算所有启用的虚拟数据点（从快照获取依赖数据）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>计算结果列表</returns>
    Task<List<VirtualNodeCalculationResult>> CalculateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取虚拟数据点的依赖 Tags
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>依赖的数据点 Tags</returns>
    List<string> ParseDependencies(string expression);
}