using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 规则执行结果
/// </summary>
public class RuleExecutionResult
{
    /// <summary>
    /// 是否通过规则检验
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// 处理后的值（可能经过转换）
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 数据质量（规则失败时可能设置为 Bad）
    /// </summary>
    public DataQuality Quality { get; set; } = DataQuality.Good;

    /// <summary>
    /// 失败消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 触发的规则列表
    /// </summary>
    public List<string> TriggeredRules { get; set; } = new();

    /// <summary>
    /// 是否应该拒绝此数据（OnFailure = Reject）
    /// </summary>
    public bool ShouldReject { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static RuleExecutionResult Ok(object? value, List<string>? triggeredRules = null)
    {
        return new RuleExecutionResult
        {
            Success = true,
            Value = value,
            TriggeredRules = triggeredRules ?? new List<string>()
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static RuleExecutionResult Fail(string errorMessage, bool shouldReject = false, object? defaultValue = null)
    {
        return new RuleExecutionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ShouldReject = shouldReject,
            Value = defaultValue,
            Quality = DataQuality.Bad
        };
    }
}

/// <summary>
/// 规则引擎接口
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// 对单个数据点执行所有适用的规则
    /// </summary>
    /// <param name="data">原始采集数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则执行结果</returns>
    Task<RuleExecutionResult> ExecuteRulesAsync(CollectedData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量执行规则
    /// </summary>
    /// <param name="dataList">原始采集数据列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则执行结果列表</returns>
    Task<List<RuleExecutionResult>> ExecuteRulesBatchAsync(IEnumerable<CollectedData> dataList, CancellationToken cancellationToken = default);
}
