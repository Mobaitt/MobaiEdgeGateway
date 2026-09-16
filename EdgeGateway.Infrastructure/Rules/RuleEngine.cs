using System.Collections.Concurrent;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Domain.Options;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NCalc;

namespace EdgeGateway.Infrastructure.Rules;

/// <summary>
/// 规则引擎实现 - 对采集数据进行限制、转换、校验和计算
///
/// 优化特性：
/// - 规则缓存：5 分钟过期，避免频繁查询数据库
/// - 主动刷新：配置变更时可主动刷新缓存
/// - 变化率校验：支持数据点级别的变化率限制
/// - 死区过滤：支持死区过滤，减少冗余数据
/// </summary>
public class RuleEngine : IRuleEngine
{
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly ILogger<RuleEngine> _logger;
    private readonly GatewayOptions _options;

    // 规则缓存：DataPointId -> 规则列表（按优先级排序）
    private readonly ConcurrentDictionary<int, List<DataPointRule>> _dataPointRulesCache = new();

    // 设备规则缓存：DeviceId -> 规则列表
    private readonly ConcurrentDictionary<int, List<DataPointRule>> _deviceRulesCache = new();

    // 全局规则缓存
    private List<DataPointRule> _globalRulesCache = new();

    // 规则缓存过期时间（从配置读取，默认 5 分钟）
    private readonly TimeSpan _cacheExpiration;
    private DateTime _lastCacheLoadTime = DateTime.MinValue;

    // 缓存加载锁（防止并发刷新）
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    // 上一次的值（用于变化率校验）
    private readonly ConcurrentDictionary<int, (object? Value, DateTime Timestamp)> _lastValues = new();
    private Func<string, object?>? _getDataSnapshot;

    public RuleEngine(
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        ILogger<RuleEngine> logger,
        IOptions<GatewayOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _options = options.Value;

        // 从配置读取规则引擎参数
        _cacheExpiration = TimeSpan.FromMinutes(_options.Rules.CacheExpirationMinutes);
    }

    public void SetDataSnapshotGetter(Func<string, object?> getDataSnapshot)
    {
        _getDataSnapshot = getDataSnapshot ?? throw new ArgumentNullException(nameof(getDataSnapshot));
    }

    public async Task<int?> GetReadTimeoutMsAsync(int deviceId, IReadOnlyCollection<int> dataPointIds)
    {
        await LoadRulesCacheAsync();

        var rules = new List<DataPointRule>(_globalRulesCache);
        if (_deviceRulesCache.TryGetValue(deviceId, out var deviceRules))
            rules.AddRange(deviceRules);

        foreach (var dataPointId in dataPointIds)
        {
            if (_dataPointRulesCache.TryGetValue(dataPointId, out var pointRules))
                rules.AddRange(pointRules);
        }

        var timeouts = new List<int>();
        foreach (var rule in rules.Where(rule => rule.RuleType == Domain.Enums.RuleType.Limit))
        {
            try
            {
                var timeout = JsonConvert.DeserializeObject<LimitRuleConfig>(rule.RuleConfig)?.ReadTimeoutMs ?? 0;
                if (timeout > 0)
                    timeouts.Add(timeout);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "规则 [{RuleName}] 的限制配置无效，忽略读取超时设置", rule.Name);
            }
        }

        return timeouts.Count > 0 ? timeouts.Min() : null;
    }

    /// <summary>
    /// 加载所有规则到缓存（线程安全，使用异步锁）
    /// </summary>
    public async Task LoadRulesCacheAsync()
    {
        if (DateTime.UtcNow - _lastCacheLoadTime < _cacheExpiration)
            return;

        await _cacheLock.WaitAsync();
        try
        {
            // 双重检查，避免重复加载
            if (DateTime.UtcNow - _lastCacheLoadTime < _cacheExpiration)
                return;

            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var allRules = await context.DataPointRules
                .Include(r => r.DataPoint)
                .Include(r => r.Device)
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .ToListAsync();

            // 启动时统一装载一次，再按作用域拆分到不同缓存，减少运行期查询数据库的次数。
            // 分类缓存
            // 全局规则：没有绑定设备和数据点的规则
            _globalRulesCache = allRules.Where(r => r.DataPointIds.Count == 0 && r.DeviceId == null).ToList();

            // 数据点规则：绑定了数据点的规则（只针对特定数据点生效）
            _dataPointRulesCache.Clear();
            foreach (var rule in allRules.Where(r => r.DataPointIds.Count > 0))
            {
                // 一个规则可能绑定多个数据点，需要为每个数据点添加规则
                foreach (var dataPointId in rule.DataPointIds)
                {
                    _dataPointRulesCache.AddOrUpdate(
                        dataPointId,
                        new List<DataPointRule> { rule },
                        (_, existing) =>
                        {
                            // 避免重复添加
                            if (!existing.Any(r => r.Id == rule.Id))
                            {
                                existing.Add(rule);
                                existing.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                            }
                            return existing;
                        });
                }
            }

            // 设备规则：只绑定设备但没有绑定数据点的规则（针对设备下所有数据点生效）
            _deviceRulesCache.Clear();
            foreach (var rule in allRules.Where(r => r.DeviceId.HasValue && r.DataPointIds.Count == 0))
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
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 主动刷新规则缓存（用于配置变更时）
    /// </summary>
    public async Task RefreshRulesCacheAsync()
    {
        _lastCacheLoadTime = DateTime.MinValue; // 强制过期
        await LoadRulesCacheAsync();
        _logger.LogInformation("规则缓存已主动刷新");
    }

    /// <summary>
    /// 清除指定数据点的规则缓存（用于配置变更时）
    /// </summary>
    public void InvalidateDataPointRulesCache(int dataPointId)
    {
        _dataPointRulesCache.TryRemove(dataPointId, out _);
        _logger.LogDebug("数据点 ID={DataPointId} 的规则缓存已清除", dataPointId);
    }

    /// <summary>
    /// 清除指定设备的规则缓存（用于配置变更时）
    /// </summary>
    public void InvalidateDeviceRulesCache(int deviceId)
    {
        _deviceRulesCache.TryRemove(deviceId, out _);
        _logger.LogDebug("设备 ID={DeviceId} 的规则缓存已清除", deviceId);
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

        // 一条采集数据可能同时命中全局规则、设备规则和数据点规则，最终按优先级串行执行。
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

                        return RuleExecutionResult.Fail(result.ErrorMessage ?? "规则执行失败", shouldReject: true);
                    }

                    if (rule.OnFailure == FailureAction.DefaultValue)
                    {
                        currentValue = GetDefaultValue(rule);
                        _logger.LogDebug("规则 [{RuleName}] 执行失败，使用默认值：{DefaultValue}",
                            rule.Name, GetDefaultValue(rule));
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

        // MaxPollingRateMs 表示同一数据点两次通过规则的最小间隔。
        if (config.MaxPollingRateMs > 0 && _lastValues.TryGetValue(data.DataPointId, out var lastValue))
        {
            var elapsedMs = (DateTime.UtcNow - lastValue.Timestamp).TotalMilliseconds;
            if (elapsedMs < config.MaxPollingRateMs)
            {
                return RuleExecutionResult.Fail(
                    $"采集间隔 {elapsedMs:F0}ms 小于限制 {config.MaxPollingRateMs}ms",
                    shouldReject: rule.OnFailure == FailureAction.Reject,
                    defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : currentValue);
            }
        }

        if (config.MaxDataPointsPerRead > 0 &&
            data.ReadBatchSize.HasValue &&
            data.ReadBatchSize.Value > config.MaxDataPointsPerRead)
        {
            return RuleExecutionResult.Fail(
                $"本次读取包含 {data.ReadBatchSize.Value} 个数据点，超过限制 {config.MaxDataPointsPerRead}",
                shouldReject: rule.OnFailure == FailureAction.Reject,
                defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : currentValue);
        }

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
                        defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : currentValue);
                }

                if (config.MaxValue.HasValue && doubleValue > config.MaxValue.Value)
                {
                    return RuleExecutionResult.Fail(
                        $"值 {doubleValue} 大于最大值 {config.MaxValue}",
                        shouldReject: rule.OnFailure == FailureAction.Reject,
                        defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : currentValue);
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
        if (config == null || config.CalculationType == Domain.Enums.CalculationType.Custom && string.IsNullOrWhiteSpace(config.Expression))
            return RuleExecutionResult.Ok(data.Value);

        var tags = config.SourceDataPointTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (tags.Count == 0 && config.CalculationType == Domain.Enums.CalculationType.Custom)
            tags = ParseCalculationDependencies(config.Expression);

        if (tags.Count == 0)
            return CalculationFailure(rule, "计算规则未配置参与计算的数据点");

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            values[tag] = string.Equals(tag, data.Tag, StringComparison.Ordinal)
                ? data.Value
                : _getDataSnapshot?.Invoke(tag);
        }

        var missing = values.Where(item => item.Value == null).Select(item => item.Key).ToList();
        if (missing.Count > 0 && config.CalculationType != Domain.Enums.CalculationType.Count)
            return CalculationFailure(rule, $"计算依赖数据未就绪：{string.Join(", ", missing)}");

        try
        {
            var numericValues = values.Values
                .Where(value => value != null)
                .Select(value => Convert.ToDouble(value))
                .ToList();

            object? result = config.CalculationType switch
            {
                Domain.Enums.CalculationType.Sum => numericValues.Sum(),
                Domain.Enums.CalculationType.Average => numericValues.Count == 0 ? null : numericValues.Average(),
                Domain.Enums.CalculationType.Max => numericValues.Count == 0 ? null : numericValues.Max(),
                Domain.Enums.CalculationType.Min => numericValues.Count == 0 ? null : numericValues.Min(),
                Domain.Enums.CalculationType.Count => numericValues.Count,
                Domain.Enums.CalculationType.StandardDeviation => CalculateStandardDeviation(numericValues),
                Domain.Enums.CalculationType.WeightedAverage => CalculateWeightedAverage(numericValues, config.Weights),
                Domain.Enums.CalculationType.Custom => EvaluateCalculationExpression(config.Expression!, values),
                _ => data.Value
            };

            if (result is null)
                return CalculationFailure(rule, "计算结果为空");

            if (result is double doubleResult)
            {
                var decimalPlaces = Math.Clamp(config.DecimalPlaces, 0, 12);
                result = Math.Round(doubleResult, decimalPlaces);
            }

            return RuleExecutionResult.Ok(ConvertCalculationResult(result, config.ResultDataType));
        }
        catch (Exception ex)
        {
            return CalculationFailure(rule, $"计算规则执行失败：{ex.Message}");
        }
    }

    private static RuleExecutionResult CalculationFailure(DataPointRule rule, string message)
    {
        return RuleExecutionResult.Fail(
            message,
            shouldReject: rule.OnFailure == FailureAction.Reject,
            defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : null);
    }

    private static List<string> ParseCalculationDependencies(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return new List<string>();

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Math", "Abs", "Sqrt", "Sin", "Cos", "Tan", "Max", "Min", "Average", "Avg",
            "Sum", "Count", "Round", "Floor", "Ceiling", "Pow", "Exp", "Log", "Log10",
            "Sign", "true", "false", "null", "and", "or", "not", "if", "then", "else"
        };

        return System.Text.RegularExpressions.Regex.Matches(
                expression,
                @"\b([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\b")
            .Select(match => match.Value)
            .Where(tag => !excluded.Contains(tag))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static double CalculateStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var average = values.Average();
        return Math.Sqrt(values.Sum(value => Math.Pow(value - average, 2)) / values.Count);
    }

    private static object? ConvertCalculationResult(object? result, Domain.Enums.DataValueType dataType)
    {
        if (result is null)
            return null;

        return dataType switch
        {
            Domain.Enums.DataValueType.Bool => Convert.ToBoolean(result),
            Domain.Enums.DataValueType.Int16 => Convert.ToInt16(result),
            Domain.Enums.DataValueType.UInt16 => Convert.ToUInt16(result),
            Domain.Enums.DataValueType.Int32 => Convert.ToInt32(result),
            Domain.Enums.DataValueType.UInt32 => Convert.ToUInt32(result),
            Domain.Enums.DataValueType.Float => Convert.ToSingle(result),
            Domain.Enums.DataValueType.Int64 => Convert.ToInt64(result),
            Domain.Enums.DataValueType.UInt64 => Convert.ToUInt64(result),
            Domain.Enums.DataValueType.String => Convert.ToString(result),
            _ => Convert.ToDouble(result)
        };
    }

    private static double CalculateWeightedAverage(IReadOnlyList<double> values, IReadOnlyList<double> weights)
    {
        if (values.Count == 0 || values.Count != weights.Count)
            throw new ArgumentException("权重数量必须与数据点数量一致");
        var totalWeight = weights.Sum();
        if (Math.Abs(totalWeight) < double.Epsilon)
            throw new ArgumentException("权重总和不能为 0");
        return values.Zip(weights, (value, weight) => value * weight).Sum() / totalWeight;
    }

    private static double EvaluateCalculationExpression(string expression, IReadOnlyDictionary<string, object?> values)
    {
        var processedExpression = expression;
        foreach (var tag in values.Keys.OrderByDescending(tag => tag.Length))
        {
            var escapedTag = System.Text.RegularExpressions.Regex.Escape(tag);
            processedExpression = System.Text.RegularExpressions.Regex.Replace(
                processedExpression,
                $"(?<!\\[)(?<!\\w){escapedTag}(?!\\w)(?!\\])",
                $"[{tag}]");
        }

        var expressionObject = new Expression(processedExpression);
        foreach (var item in values)
            expressionObject.Parameters[item.Key] = Convert.ToDouble(item.Value ?? 0);

        return Convert.ToDouble(expressionObject.Evaluate());
    }

    #region 辅助方法

    /// <summary>
    /// 从 DefaultValueJson 反序列化获取默认值
    /// </summary>
    private static object? GetDefaultValue(DataPointRule rule)
    {
        return rule.DefaultValueJson != null 
            ? System.Text.Json.JsonSerializer.Deserialize<object>(rule.DefaultValueJson) 
            : null;
    }

    private RuleExecutionResult ValidateRange(ValidationRuleConfig config, double value, DataPointRule rule)
    {
        if (config.MinValue.HasValue && value < config.MinValue.Value)
        {
            return RuleExecutionResult.Fail(
                $"值 {value} 小于最小值 {config.MinValue}",
                shouldReject: rule.OnFailure == FailureAction.Reject,
                defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : value);
        }

        if (config.MaxValue.HasValue && value > config.MaxValue.Value)
        {
            return RuleExecutionResult.Fail(
                $"值 {value} 大于最大值 {config.MaxValue}",
                shouldReject: rule.OnFailure == FailureAction.Reject,
                defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : value);
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

        // 变化率校验依赖上一条成功通过规则的数据，因此这里读取内部缓存而不是原始设备值。
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
                            defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : currentValue);
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

        // 死区过滤返回上一值而不是拒绝数据，这样发送链路看到的是平滑后的连续值。
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
                    defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : value);
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
                defaultValue: rule.OnFailure == FailureAction.DefaultValue ? GetDefaultValue(rule) : value);
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

        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ulong.TryParse(text[2..], System.Globalization.NumberStyles.AllowHexSpecifier,
                    System.Globalization.CultureInfo.InvariantCulture, out var prefixedHex))
            {
                result = prefixedHex;
                return true;
            }

            // Modbus Hex 点位以字符串返回（例如 "307C"），范围/变化率校验仍应按数值执行。
            var hasHexLetter = text.Any(char.IsLetter);
            var hasDigit = text.Any(char.IsDigit);
            if (hasHexLetter && hasDigit &&
                ulong.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier,
                    System.Globalization.CultureInfo.InvariantCulture, out var hexValue))
            {
                result = hexValue;
                return true;
            }

            if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase) &&
                ConvertBinaryToDouble(text[2..], out result))
                return true;

            if (text.Length > 2 && text.All(character => character is '0' or '1') &&
                ConvertBinaryToDouble(text, out result))
                return true;
        }

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

    private static bool ConvertBinaryToDouble(string text, out double result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text) || text.Any(character => character is not ('0' or '1')))
            return false;

        try
        {
            result = Convert.ToInt64(text, 2);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    #endregion
}
