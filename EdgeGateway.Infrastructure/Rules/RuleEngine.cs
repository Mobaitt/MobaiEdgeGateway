using System.Collections.Concurrent;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EdgeGateway.Infrastructure.Rules;

/// <summary>
/// 规则引擎实现 - 对采集数据进行限制、转换、校验和计算
/// </summary>
public class RuleEngine : IRuleEngine
{
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly ILogger<RuleEngine> _logger;

    // 规则缓存：DataPointId -> 规则列表（按优先级排序）
    private readonly ConcurrentDictionary<int, List<DataPointRule>> _dataPointRulesCache = new();

    // 设备规则缓存：DeviceId -> 规则列表
    private readonly ConcurrentDictionary<int, List<DataPointRule>> _deviceRulesCache = new();

    // 全局规则缓存
    private List<DataPointRule> _globalRulesCache = new();

    // 规则缓存过期时间（5 分钟）
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private DateTime _lastCacheLoadTime = DateTime.MinValue;

    // 上一次的值（用于变化率校验）
    private readonly ConcurrentDictionary<int, (object? Value, DateTime Timestamp)> _lastValues = new();

    public RuleEngine(
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        ILogger<RuleEngine> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// 加载所有规则到缓存
    /// </summary>
    private async Task LoadRulesCacheAsync()
    {
        if (DateTime.UtcNow - _lastCacheLoadTime < _cacheExpiration)
            return;

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var allRules = await context.DataPointRules
            .Include(r => r.DataPoint)
            .Include(r => r.Device)
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        // 分类缓存
        _globalRulesCache = allRules.Where(r => r.DataPointId == null && r.DeviceId == null).ToList();

        _dataPointRulesCache.Clear();
        foreach (var rule in allRules.Where(r => r.DataPointId.HasValue))
        {
            if (rule.DataPointId.HasValue)
            {
                _dataPointRulesCache.AddOrUpdate(
                    rule.DataPointId.Value,
                    new List<DataPointRule> { rule },
                    (_, existing) =>
                    {
                        existing.Add(rule);
                        existing.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                        return existing;
                    });
            }
        }

        _deviceRulesCache.Clear();
        foreach (var rule in allRules.Where(r => r.DeviceId.HasValue))
        {
            if (rule.DeviceId.HasValue)
            {
                _deviceRulesCache.AddOrUpdate(
                    rule.DeviceId.Value,
                    new List<DataPointRule> { rule },
                    (_, existing) =>
                    {
                        existing.Add(rule);
                        existing.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                        return existing;
                    });
            }
        }

        _lastCacheLoadTime = DateTime.UtcNow;
        _logger.LogInformation("规则缓存已加载：全局 {_globalRules} 条，数据点 {_dataPointRules} 条，设备 {_deviceRules} 条",
            _globalRulesCache.Count, _dataPointRulesCache.Count, _deviceRulesCache.Count);
    }

    /// <summary>
    /// 获取适用于某个数据点的所有规则
    /// </summary>
    private List<DataPointRule> GetApplicableRules(CollectedData data)
    {
        var rules = new List<DataPointRule>();

        // 添加全局规则
        rules.AddRange(_globalRulesCache);

        // 添加数据点规则
        if (_dataPointRulesCache.TryGetValue(data.DataPointId, out var dpRules))
        {
            rules.AddRange(dpRules);
        }

        // 添加设备规则
        if (_deviceRulesCache.TryGetValue(data.DeviceId, out var devRules))
        {
            rules.AddRange(devRules);
        }

        // 按优先级排序
        rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return rules;
    }

    public async Task<RuleExecutionResult> ExecuteRulesAsync(CollectedData data, CancellationToken cancellationToken = default)
    {
        await LoadRulesCacheAsync();

        var applicableRules = GetApplicableRules(data);
        var triggeredRules = new List<string>();
        object? currentValue = data.Value;

        foreach (var rule in applicableRules)
        {
            try
            {
                var result = await ExecuteSingleRuleAsync(rule, data, currentValue, cancellationToken);

                if (!result.Success)
                {
                    triggeredRules.Add(rule.Name);

                    // 根据失败处理方式处理
                    if (result.ShouldReject)
                    {
                        _logger.LogWarning("规则 [{RuleName}] 拒绝数据：{Tag}, 值：{Value}, 原因：{Reason}",
                            rule.Name, data.Tag, currentValue, result.ErrorMessage);

                        return RuleExecutionResult.Fail(result.ErrorMessage, shouldReject: true);
                    }

                    if (rule.OnFailure == FailureAction.DefaultValue)
                    {
                        currentValue = rule.DefaultValue;
                        _logger.LogDebug("规则 [{RuleName}] 执行失败，使用默认值：{DefaultValue}",
                            rule.Name, rule.DefaultValue);
                    }
                    else if (rule.OnFailure == FailureAction.Pass)
                    {
                        _logger.LogDebug("规则 [{RuleName}] 执行失败，放行数据：{Tag}", rule.Name, data.Tag);
                    }
                }
                else
                {
                    // 规则执行成功，更新当前值
                    if (result.Value != null)
                    {
                        currentValue = result.Value;
                    }
                    triggeredRules.Add(rule.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "规则 [{RuleName}] 执行异常", rule.Name);

                if (rule.OnFailure == FailureAction.Reject)
                {
                    return RuleExecutionResult.Fail($"规则执行异常：{ex.Message}", shouldReject: true);
                }
            }
        }

        // 更新最后值（用于变化率校验）
        _lastValues.AddOrUpdate(data.DataPointId,
            (currentValue, DateTime.UtcNow),
            (_, _) => (currentValue, DateTime.UtcNow));

        return RuleExecutionResult.Ok(currentValue, triggeredRules);
    }

    public async Task<List<RuleExecutionResult>> ExecuteRulesBatchAsync(IEnumerable<CollectedData> dataList, CancellationToken cancellationToken = default)
    {
        var results = new List<RuleExecutionResult>();

        foreach (var data in dataList)
        {
            var result = await ExecuteRulesAsync(data, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 执行单条规则
    /// </summary>
    private async Task<RuleExecutionResult> ExecuteSingleRuleAsync(
        DataPointRule rule,
        CollectedData data,
        object? currentValue,
        CancellationToken cancellationToken)
    {
        return rule.RuleType switch
        {
            Domain.Enums.RuleType.Limit => ExecuteLimitRule(rule, data, currentValue),
            Domain.Enums.RuleType.Transform => ExecuteTransformRule(rule, currentValue),
            Domain.Enums.RuleType.Validation => await ExecuteValidationRuleAsync(rule, data, currentValue, cancellationToken),
            Domain.Enums.RuleType.Calculation => await ExecuteCalculationRuleAsync(rule, data, cancellationToken),
            _ => RuleExecutionResult.Ok(currentValue)
        };
    }

    /// <summary>
    /// 执行限制规则
    /// </summary>
    private RuleExecutionResult ExecuteLimitRule(DataPointRule rule, CollectedData data, object? currentValue)
    {
        var config = JsonConvert.DeserializeObject<LimitRuleConfig>(rule.RuleConfig);
        if (config == null)
            return RuleExecutionResult.Ok(currentValue);

        // 数值范围限制
        if (config.MinValue.HasValue || config.MaxValue.HasValue)
        {
            if (TryConvertToDouble(currentValue, out var doubleValue))
            {
                if (config.MinValue.HasValue && doubleValue < config.MinValue.Value)
                {
                    return RuleExecutionResult.Fail(
                        $"值 {doubleValue} 小于最小值 {config.MinValue}",
                        shouldReject: rule.OnFailure == FailureAction.Reject,
                        defaultValue: rule.OnFailure == FailureAction.DefaultValue ? rule.DefaultValue : currentValue);
                }

                if (config.MaxValue.HasValue && doubleValue > config.MaxValue.Value)
                {
                    return RuleExecutionResult.Fail(
                        $"值 {doubleValue} 大于最大值 {config.MaxValue}",
                        shouldReject: rule.OnFailure == FailureAction.Reject,
                        defaultValue: rule.OnFailure == FailureAction.DefaultValue ? rule.DefaultValue : currentValue);
                }
            }
        }

        return RuleExecutionResult.Ok(currentValue);
    }

    /// <summary>
    /// 执行转换规则
    /// </summary>
    private RuleExecutionResult ExecuteTransformRule(DataPointRule rule, object? currentValue)
    {
        var config = JsonConvert.DeserializeObject<TransformRuleConfig>(rule.RuleConfig);
        if (config == null || config.TransformType == Domain.Enums.TransformType.None)
            return RuleExecutionResult.Ok(currentValue);

        if (!TryConvertToDouble(currentValue, out var doubleValue))
        {
            return RuleExecutionResult.Ok(currentValue); // 非数值类型无法转换
        }

        double result = config.TransformType switch
        {
            Domain.Enums.TransformType.Linear => doubleValue * config.Scale + config.Offset,
            Domain.Enums.TransformType.Polynomial => CalculatePolynomial(doubleValue, config.PolynomialCoefficients),
            Domain.Enums.TransformType.LookupTable => CalculateLookupTable(doubleValue, config.LookupTable),
            Domain.Enums.TransformType.Formula => CalculateFormula(config.Formula, doubleValue),
            Domain.Enums.TransformType.UnitConversion => ConvertUnit(doubleValue, config.FromUnit, config.ToUnit),
            _ => doubleValue
        };

        return RuleExecutionResult.Ok(result);
    }

    /// <summary>
    /// 执行校验规则
    /// </summary>
    private async Task<RuleExecutionResult> ExecuteValidationRuleAsync(
        DataPointRule rule,
        CollectedData data,
        object? currentValue,
        CancellationToken cancellationToken)
    {
        var config = JsonConvert.DeserializeObject<ValidationRuleConfig>(rule.RuleConfig);
        if (config == null || config.ValidationType == Domain.Enums.ValidationType.None)
            return RuleExecutionResult.Ok(currentValue);

        if (!TryConvertToDouble(currentValue, out var doubleValue))
        {
            return RuleExecutionResult.Ok(currentValue);
        }

        return config.ValidationType switch
        {
            Domain.Enums.ValidationType.Range => ValidateRange(config, doubleValue, rule),
            Domain.Enums.ValidationType.RateOfChange => await ValidateRateOfChangeAsync(rule, data, doubleValue, config, cancellationToken),
            Domain.Enums.ValidationType.DeadBand => ValidateDeadBand(rule, data, doubleValue, config),
            Domain.Enums.ValidationType.Rationality => ValidateRationality(config, doubleValue, rule),
            Domain.Enums.ValidationType.FixedValue => ValidateFixedValue(config, doubleValue, rule),
            _ => RuleExecutionResult.Ok(currentValue)
        };
    }

    /// <summary>
    /// 执行计算规则
    /// </summary>
    private async Task<RuleExecutionResult> ExecuteCalculationRuleAsync(
        DataPointRule rule,
        CollectedData data,
        CancellationToken cancellationToken)
    {
        var config = JsonConvert.DeserializeObject<CalculationRuleConfig>(rule.RuleConfig);
        if (config == null)
            return RuleExecutionResult.Ok(data.Value);

        // 计算规则通常需要多个数据点，这里简化处理
        // 实际使用时，计算规则应该由专门的服务来处理
        return RuleExecutionResult.Ok(data.Value);
    }

    #region 辅助方法

    private RuleExecutionResult ValidateRange(ValidationRuleConfig config, double value, DataPointRule rule)
    {
        if (config.MinValue.HasValue && value < config.MinValue.Value)
        {
            return RuleExecutionResult.Fail(
                $"值 {value} 小于最小值 {config.MinValue}",
                shouldReject: rule.OnFailure == FailureAction.Reject,
                defaultValue: rule.OnFailure == FailureAction.DefaultValue ? rule.DefaultValue : value);
        }

        if (config.MaxValue.HasValue && value > config.MaxValue.Value)
        {
            return RuleExecutionResult.Fail(
                $"值 {value} 大于最大值 {config.MaxValue}",
                shouldReject: rule.OnFailure == FailureAction.Reject,
                defaultValue: rule.OnFailure == FailureAction.DefaultValue ? rule.DefaultValue : value);
        }

        return RuleExecutionResult.Ok(value);
    }

    private async Task<RuleExecutionResult> ValidateRateOfChangeAsync(
        DataPointRule rule,
        CollectedData data,
        double currentValue,
        ValidationRuleConfig config,
        CancellationToken cancellationToken)
    {
        if (!config.MaxRateOfChange.HasValue)
            return RuleExecutionResult.Ok(currentValue);

        if (_lastValues.TryGetValue(data.DataPointId, out var lastValue))
        {
            var timeSpan = (DateTime.UtcNow - lastValue.Timestamp).TotalSeconds;
            if (timeSpan > 0)
            {
                if (TryConvertToDouble(lastValue.Value, out var lastDoubleValue))
                {
                    var rateOfChange = Math.Abs(currentValue - lastDoubleValue) / timeSpan;
                    if (rateOfChange > config.MaxRateOfChange.Value)
                    {
                        return RuleExecutionResult.Fail(
                            $"变化率 {rateOfChange:F4}/s 超过最大变化率 {config.MaxRateOfChange}/s",
                            shouldReject: rule.OnFailure == FailureAction.Reject,
                            defaultValue: rule.OnFailure == FailureAction.DefaultValue ? rule.DefaultValue : currentValue);
                    }
                }
            }
        }

        return RuleExecutionResult.Ok(currentValue);
    }

    private RuleExecutionResult ValidateDeadBand(DataPointRule rule, CollectedData data, double currentValue, ValidationRuleConfig config)
    {
        if (!config.DeadBand.HasValue)
            return RuleExecutionResult.Ok(currentValue);

        if (_lastValues.TryGetValue(data.DataPointId, out var lastValue))
        {
            if (TryConvertToDouble(lastValue.Value, out var lastDoubleValue))
            {
                if (Math.Abs(currentValue - lastDoubleValue) <= config.DeadBand.Value)
                {
                    // 变化在死区内，保持原值
                    return RuleExecutionResult.Ok(lastDoubleValue);
                }
            }
        }

        return RuleExecutionResult.Ok(currentValue);
    }

    private RuleExecutionResult ValidateRationality(ValidationRuleConfig config, double value, DataPointRule rule)
    {
        if (string.IsNullOrEmpty(config.RationalityExpression))
            return RuleExecutionResult.Ok(value);

        try
        {
            // 使用 NCalc 评估合理性表达式
            var expr = new NCalc.Expression(config.RationalityExpression.Replace("value", value.ToString()));
            var result = expr.Evaluate();

            if (result is bool isValid && !isValid)
            {
                return RuleExecutionResult.Fail(
                    $"值 {value} 未通过合理性校验",
                    shouldReject: rule.OnFailure == FailureAction.Reject,
                    defaultValue: rule.OnFailure == FailureAction.DefaultValue ? rule.DefaultValue : value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合理性校验表达式执行失败：{Expression}", config.RationalityExpression);
        }

        return RuleExecutionResult.Ok(value);
    }

    private RuleExecutionResult ValidateFixedValue(ValidationRuleConfig config, double value, DataPointRule rule)
    {
        if (!config.FixedValue.HasValue)
            return RuleExecutionResult.Ok(value);

        var tolerance = config.FixedValueTolerance ?? 0.001;
        if (Math.Abs(value - config.FixedValue.Value) > tolerance)
        {
            return RuleExecutionResult.Fail(
                $"值 {value} 与固定值 {config.FixedValue} 的偏差超过容差 {tolerance}",
                shouldReject: rule.OnFailure == FailureAction.Reject,
                defaultValue: rule.OnFailure == FailureAction.DefaultValue ? rule.DefaultValue : value);
        }

        return RuleExecutionResult.Ok(value);
    }

    private static double CalculatePolynomial(double x, double[]? coefficients)
    {
        if (coefficients == null || coefficients.Length == 0)
            return x;

        double result = 0;
        foreach (var coef in coefficients)
        {
            result = result * x + coef;
        }
        return result;
    }

    private static double CalculateLookupTable(double x, Dictionary<double, double>? lookupTable)
    {
        if (lookupTable == null || lookupTable.Count == 0)
            return x;

        // 查找最接近的点
        var keys = lookupTable.Keys.OrderBy(k => k).ToList();
        if (x <= keys.First()) return lookupTable[keys.First()];
        if (x >= keys.Last()) return lookupTable[keys.Last()];

        // 线性插值
        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (x >= keys[i] && x <= keys[i + 1])
            {
                var t = (x - keys[i]) / (keys[i + 1] - keys[i]);
                return lookupTable[keys[i]] + t * (lookupTable[keys[i + 1]] - lookupTable[keys[i]]);
            }
        }

        return x;
    }

    private static double CalculateFormula(string? formula, double x)
    {
        if (string.IsNullOrEmpty(formula))
            return x;

        try
        {
            var expr = new NCalc.Expression(formula.Replace("x", x.ToString()));
            return Convert.ToDouble(expr.Evaluate());
        }
        catch
        {
            return x;
        }
    }

    private static double ConvertUnit(double value, string? fromUnit, string? toUnit)
    {
        // 简化的单位转换，实际项目中需要更完善的单位转换逻辑
        if (string.IsNullOrEmpty(fromUnit) || string.IsNullOrEmpty(toUnit))
            return value;

        // 摄氏度 <-> 华氏度
        if (fromUnit == "℃" && toUnit == "℉")
            return value * 9 / 5 + 32;
        if (fromUnit == "℉" && toUnit == "℃")
            return (value - 32) * 5 / 9;

        // MPa <-> bar
        if (fromUnit == "MPa" && toUnit == "bar")
            return value * 10;
        if (fromUnit == "bar" && toUnit == "MPa")
            return value / 10;

        return value;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;
        if (value == null)
            return false;

        try
        {
            result = Convert.ToDouble(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
