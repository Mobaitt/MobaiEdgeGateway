using System.Collections.Concurrent;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NCalc;

namespace EdgeGateway.Infrastructure.VirtualNodes;

/// <summary>
/// 虚拟节点计算引擎实现 - 根据表达式计算虚拟数据点的值
/// 虚拟节点依附于普通设备，可以像普通数据点一样被管理和发送
/// </summary>
public class VirtualNodeEngine : IVirtualNodeEngine
{
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly ILogger<VirtualNodeEngine> _logger;
    private readonly IRuleEngine? _ruleEngine;
    private Func<string, object?>? _getDataSnapshot; // 获取快照数据的委托

    // 虚拟数据点缓存
    private readonly ConcurrentDictionary<int, VirtualDataPoint> _virtualPointCache = new();

    // 设备 ID -> 该设备下的虚拟数据点 ID 列表
    private readonly ConcurrentDictionary<int, List<int>> _deviceVirtualPointIndex = new();

    // 依赖关系索引：真实数据点 Tag -> 依赖它的虚拟数据点 ID 列表
    private readonly ConcurrentDictionary<string, List<int>> _dependencyIndex = new();

    // 计算锁（防止同一虚拟点并发计算）
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _calculationLocks = new();

    public VirtualNodeEngine(
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        ILogger<VirtualNodeEngine> logger,
        IRuleEngine? ruleEngine = null,
        Func<string, object?>? getDataSnapshot = null)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _ruleEngine = ruleEngine;
        _getDataSnapshot = getDataSnapshot;
    }
    
    /// <summary>
    /// 设置获取快照数据的委托（由 DataCollectionService 设置）
    /// </summary>
    public void SetDataSnapshotGetter(Func<string, object?> getDataSnapshot)
    {
        _getDataSnapshot = getDataSnapshot;
    }

    /// <summary>
    /// 加载缓存
    /// </summary>
    private async Task LoadCacheAsync()
    {
        if (_virtualPointCache.Any())
            return;

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        // 加载虚拟数据点
        var virtualPoints = await context.VirtualDataPoints
            .Include(p => p.Device)
            .Where(p => p.IsEnabled)
            .ToListAsync();

        foreach (var point in virtualPoints)
        {
            _virtualPointCache[point.Id] = point;

            // 构建设备索引
            _deviceVirtualPointIndex.AddOrUpdate(
                point.DeviceId,
                new List<int> { point.Id },
                (_, existing) =>
                {
                    if (!existing.Contains(point.Id))
                        existing.Add(point.Id);
                    return existing;
                });

            // 构建依赖索引
            var dependencies = ParseDependencies(point.Expression);
            foreach (var depTag in dependencies)
            {
                _dependencyIndex.AddOrUpdate(
                    depTag,
                    new List<int> { point.Id },
                    (_, existing) =>
                    {
                        if (!existing.Contains(point.Id))
                            existing.Add(point.Id);
                        return existing;
                    });
            }
        }

        _logger.LogInformation("虚拟节点缓存已加载：{_pointCount} 个虚拟数据点，{_deviceCount} 个设备有虚拟节点",
            _virtualPointCache.Count, _deviceVirtualPointIndex.Count);
    }

    /// <summary>
    /// 刷新缓存
    /// </summary>
    public async Task RefreshCacheAsync()
    {
        _virtualPointCache.Clear();
        _deviceVirtualPointIndex.Clear();
        _dependencyIndex.Clear();
        await LoadCacheAsync();
    }

    public async Task<VirtualNodeCalculationResult> CalculateAsync(VirtualDataPoint virtualDataPoint, CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取锁
            var semaphore = _calculationLocks.GetOrAdd(virtualDataPoint.Id, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // 解析依赖数据
                var dependencies = ParseDependencies(virtualDataPoint.Expression);
                var dependencyValues = new Dictionary<string, object?>();

                foreach (var depTag in dependencies)
                {
                    // 从快照中获取最新值（通过委托）
                    if (_getDataSnapshot != null)
                    {
                        var value = _getDataSnapshot(depTag);
                        if (value != null)
                        {
                            dependencyValues[depTag] = value;
                            _logger.LogInformation("虚拟数据点 {VirtualTag} 使用快照数据：{DepTag} = {Value}", 
                                virtualDataPoint.Tag, depTag, value);
                            continue;
                        }
                        else
                        {
                            _logger.LogWarning("虚拟数据点 {VirtualTag} 依赖数据 {DepTag} 在快照中为 null 或超时", 
                                virtualDataPoint.Tag, depTag);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("虚拟数据点 {VirtualTag} 的 getDataSnapshot 委托未设置", 
                            virtualDataPoint.Tag);
                    }
                    
                    // 快照中没有，从数据库查找
                    await using var context = await _dbContextFactory.CreateDbContextAsync();
                    var dataPoint = await context.DataPoints
                        .FirstOrDefaultAsync(dp => dp.Tag == depTag, cancellationToken);

                    if (dataPoint != null)
                    {
                        // 数据库中有定义，但快照中没有值
                        dependencyValues[depTag] = null;
                        _logger.LogDebug("虚拟数据点 {VirtualTag} 依赖数据 {DepTag} 在快照中缺失", 
                            virtualDataPoint.Tag, depTag);
                    }
                    else
                    {
                        // 检查是否是其他虚拟数据点
                        var virtualPoint = _virtualPointCache.Values.FirstOrDefault(vp => vp.Tag == depTag);
                        if (virtualPoint != null)
                        {
                            dependencyValues[depTag] = virtualPoint.LastValue;
                        }
                        else
                        {
                            dependencyValues[depTag] = null;
                            _logger.LogDebug("虚拟数据点 {VirtualTag} 依赖数据 {DepTag} 未找到", 
                                virtualDataPoint.Tag, depTag);
                        }
                    }
                }

                // 检查依赖数据是否完整
                var missingDependencies = dependencies.Where(d => !dependencyValues.ContainsKey(d) || dependencyValues[d] == null).ToList();
                if (missingDependencies.Any() && dependencies.Any())
                {
                    _logger.LogWarning("虚拟数据点 {Tag} 缺少依赖数据：{MissingTags}",
                        virtualDataPoint.Tag, string.Join(", ", missingDependencies));
                }

                // 执行计算
                var result = ExecuteCalculation(virtualDataPoint, dependencyValues);

                // 更新虚拟数据点的最后计算结果
                await using var updateContext = await _dbContextFactory.CreateDbContextAsync();
                var pointToUpdate = await updateContext.VirtualDataPoints.FindAsync(virtualDataPoint.Id);
                if (pointToUpdate != null)
                {
                    pointToUpdate.LastValue = result.Value;
                    pointToUpdate.LastCalculationTime = DateTime.UtcNow;
                    await updateContext.SaveChangesAsync(cancellationToken);
                }

                _logger.LogDebug("虚拟数据点 {Tag} 计算完成：{Value}", virtualDataPoint.Tag, result.Value);

                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟数据点 {Tag} 计算失败", virtualDataPoint.Tag);
            return VirtualNodeCalculationResult.Fail($"计算失败：{ex.Message}");
        }
    }

    public async Task<List<VirtualNodeCalculationResult>> CalculateDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        await LoadCacheAsync();

        if (!_deviceVirtualPointIndex.TryGetValue(deviceId, out var virtualPointIds))
        {
            _logger.LogWarning("设备 ID={DeviceId} 没有虚拟数据点", deviceId);
            return new List<VirtualNodeCalculationResult>();
        }

        var results = new List<VirtualNodeCalculationResult>();

        foreach (var pointId in virtualPointIds)
        {
            if (_virtualPointCache.TryGetValue(pointId, out var point))
            {
                var result = await CalculateAsync(point, cancellationToken);
                results.Add(result);
            }
        }

        _logger.LogInformation("设备 ID={DeviceId} 虚拟节点计算完成：{SuccessCount}/{TotalCount} 成功",
            deviceId, results.Count(r => r.Success), results.Count);

        return results;
    }

    public async Task<List<VirtualNodeCalculationResult>> CalculateAllAsync(CancellationToken cancellationToken = default)
    {
        await LoadCacheAsync();

        var virtualPoints = _virtualPointCache.Values.ToList();
        var results = new List<VirtualNodeCalculationResult>();

        foreach (var point in virtualPoints)
        {
            var result = await CalculateAsync(point, cancellationToken);
            results.Add(result);
        }

        _logger.LogInformation("虚拟节点计算完成：{SuccessCount}/{TotalCount} 成功",
            results.Count(r => r.Success), results.Count);

        return results;
    }

    public async Task<List<VirtualNodeCalculationResult>> OnDependencyDataUpdatedAsync(string dataPointTag, object? value, CancellationToken cancellationToken = default)
    {
        await LoadCacheAsync();

        // 查找依赖此数据点的所有虚拟数据点
        if (!_dependencyIndex.TryGetValue(dataPointTag, out var dependentVirtualPointIds))
        {
            return new List<VirtualNodeCalculationResult>();
        }

        var results = new List<VirtualNodeCalculationResult>();

        foreach (var virtualPointId in dependentVirtualPointIds)
        {
            if (_virtualPointCache.TryGetValue(virtualPointId, out var virtualPoint))
            {
                try
                {
                    var result = await CalculateAsync(virtualPoint, cancellationToken);
                    results.Add(result);

                    // 递归触发下游虚拟节点
                    if (result.Success)
                    {
                        var downstreamResults = await OnDependencyDataUpdatedAsync(virtualPoint.Tag, result.Value, cancellationToken);
                        results.AddRange(downstreamResults);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "虚拟数据点 {Tag} 因依赖更新触发计算失败", virtualPoint.Tag);
                }
            }
        }

        if (results.Any())
        {
            _logger.LogDebug("依赖数据 {Tag} 更新，触发 {Count} 个虚拟数据点计算", dataPointTag, results.Count);
        }

        return results;
    }

    public List<string> ParseDependencies(string expression)
    {
        var dependencies = new List<string>();

        if (string.IsNullOrEmpty(expression))
            return dependencies;

        // 匹配 Tag 格式：字母/下划线开头，可包含字母、数字、下划线、点
        // 支持格式：Point1, Device.Tag, Device.SubDevice.Tag 等
        var pattern = @"\b([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\b";
        var matches = System.Text.RegularExpressions.Regex.Matches(expression, pattern);

        // 排除常见函数和关键字
        var excludeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Math", "Abs", "Sqrt", "Sin", "Cos", "Tan", "Asin", "Acos", "Atan",
            "Max", "Min", "Avg", "Average", "Sum", "Count", "Round", "Floor", "Ceiling",
            "Pow", "Exp", "Log", "Log10", "Sign",
            "true", "false", "null", "and", "or", "not",
            "if", "then", "else"
        };

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var tag = match.Value;
            if (!excludeKeywords.Contains(tag))
            {
                dependencies.Add(tag);
            }
        }

        _logger.LogInformation("表达式 '{Expression}' 解析到依赖：{Dependencies}", 
            expression, string.Join(", ", dependencies));
        
        return dependencies.Distinct().ToList();
    }

    /// <summary>
    /// 执行计算
    /// </summary>
    private VirtualNodeCalculationResult ExecuteCalculation(VirtualDataPoint virtualPoint, Dictionary<string, object?> dependencyValues)
    {
        try
        {
            object? result = virtualPoint.CalculationType switch
            {
                CalculationType.Sum => CalculateSum(dependencyValues),
                CalculationType.Average => CalculateAverage(dependencyValues),
                CalculationType.Max => CalculateMax(dependencyValues),
                CalculationType.Min => CalculateMin(dependencyValues),
                CalculationType.Count => CalculateCount(dependencyValues),
                CalculationType.WeightedAverage => CalculateWeightedAverage(dependencyValues, virtualPoint.Expression),
                CalculationType.Custom => CalculateCustomExpression(virtualPoint.Expression, dependencyValues),
                _ => CalculateCustomExpression(virtualPoint.Expression, dependencyValues)
            };

            // 四舍五入到指定小数位
            if (result is double doubleResult)
            {
                result = Math.Round(doubleResult, 4);
            }

            return VirtualNodeCalculationResult.Ok(result, dependencyValues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟数据点 {Tag} 表达式计算失败：{Expression}", virtualPoint.Tag, virtualPoint.Expression);
            return VirtualNodeCalculationResult.Fail($"表达式计算失败：{ex.Message}");
        }
    }

    private object? CalculateSum(Dictionary<string, object?> values)
    {
        var numericValues = GetNumericValues(values);
        return numericValues.Sum();
    }

    private object? CalculateAverage(Dictionary<string, object?> values)
    {
        var numericValues = GetNumericValues(values);
        return numericValues.Any() ? numericValues.Average() : null;
    }

    private object? CalculateMax(Dictionary<string, object?> values)
    {
        var numericValues = GetNumericValues(values);
        return numericValues.Any() ? numericValues.Max() : null;
    }

    private object? CalculateMin(Dictionary<string, object?> values)
    {
        var numericValues = GetNumericValues(values);
        return numericValues.Any() ? numericValues.Min() : null;
    }

    private object? CalculateCount(Dictionary<string, object?> values)
    {
        return values.Count(v => v.Value != null);
    }

    private object? CalculateWeightedAverage(Dictionary<string, object?> values, string expression)
    {
        // 简化处理：假设表达式格式为 "tag1*weight1 + tag2*weight2 + ..."
        return CalculateCustomExpression(expression, values);
    }

    private object? CalculateCustomExpression(string expression, Dictionary<string, object?> values)
    {
        if (string.IsNullOrEmpty(expression))
            return null;

        // 处理表达式：将带点号的 Tag 名用方括号包裹，使 NCalc 能正确识别
        var processedExpression = expression;
        
        // 按长度降序排序，避免短 Tag 名替换长 Tag 名的问题
        var sortedTags = values.Keys.OrderByDescending(t => t.Length).ToList();
        
        foreach (var tag in sortedTags)
        {
            // 将表达式中的 Tag 名替换为方括号格式 [Tag.Name]
            var escapedTag = System.Text.RegularExpressions.Regex.Escape(tag);
            processedExpression = System.Text.RegularExpressions.Regex.Replace(
                processedExpression,
                $@"(?<!\[)(?<!\w){escapedTag}(?!\w)(?!\])",
                $"[{tag}]"
            );
        }

        // 创建 NCalc 表达式
        var ncalcExpr = new Expression(processedExpression);

        // 绑定参数
        foreach (var kvp in values)
        {
            if (kvp.Value != null)
            {
                try
                {
                    ncalcExpr.Parameters[kvp.Key] = Convert.ToDouble(kvp.Value);
                }
                catch
                {
                    // 无法转换为 double，使用 0
                    ncalcExpr.Parameters[kvp.Key] = 0;
                }
            }
            else
            {
                ncalcExpr.Parameters[kvp.Key] = 0;
            }
        }

        // 添加常用数学函数
        ncalcExpr.EvaluateFunction += (name, args) =>
        {
            switch (name.ToLower())
            {
                case "abs":
                    args.Result = Math.Abs(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    break;
                case "sqrt":
                    args.Result = Math.Sqrt(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    break;
                case "sin":
                    args.Result = Math.Sin(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    break;
                case "cos":
                    args.Result = Math.Cos(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    break;
                case "tan":
                    args.Result = Math.Tan(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    break;
                case "max":
                    args.Result = args.Parameters.Max(p => Convert.ToDouble(p.Evaluate()));
                    break;
                case "min":
                    args.Result = args.Parameters.Min(p => Convert.ToDouble(p.Evaluate()));
                    break;
                case "avg":
                case "average":
                    args.Result = args.Parameters.Average(p => Convert.ToDouble(p.Evaluate()));
                    break;
                case "sum":
                    args.Result = args.Parameters.Sum(p => Convert.ToDouble(p.Evaluate()));
                    break;
                case "round":
                    var val = Convert.ToDouble(args.Parameters[0].Evaluate());
                    var digits = args.Parameters.Length > 1 ? Convert.ToInt32(args.Parameters[1].Evaluate()) : 2;
                    args.Result = Math.Round(val, digits);
                    break;
                case "pow":
                    args.Result = Math.Pow(Convert.ToDouble(args.Parameters[0].Evaluate()), Convert.ToDouble(args.Parameters[1].Evaluate()));
                    break;
                case "log":
                    args.Result = Math.Log(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    break;
                case "log10":
                    args.Result = Math.Log10(Convert.ToDouble(args.Parameters[0].Evaluate()));
                    break;
            }
        };

        var result = ncalcExpr.Evaluate();
        return Convert.ToDouble(result);
    }

    private List<double> GetNumericValues(Dictionary<string, object?> values)
    {
        var result = new List<double>();
        foreach (var kvp in values)
        {
            if (kvp.Value != null)
            {
                try
                {
                    result.Add(Convert.ToDouble(kvp.Value));
                }
                catch
                {
                    // 忽略无法转换的值
                }
            }
        }
        return result;
    }
}
