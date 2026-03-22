using System.Collections.Concurrent;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using NCalc;
using Microsoft.Extensions.DependencyInjection;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EdgeGateway.Infrastructure.VirtualNodes;

/// <summary>
/// 虚拟节点计算引擎实现 - 根据表达式计算虚拟数据点的值
/// 虚拟节点依附于普通设备，可以像普通数据点一样被管理和发送
/// </summary>
public class VirtualNodeEngine : IVirtualNodeEngine
{
    private readonly ILogger<VirtualNodeEngine> _logger;
    private readonly IServiceProvider _serviceProvider;
    private Func<string, object?>? _getDataSnapshot; // 获取快照数据的委托

    // 虚拟数据点缓存
    private readonly ConcurrentDictionary<int, VirtualDataPoint> _virtualPointCache = new();

    // 依赖关系缓存：VirtualDataPointId -> 依赖的 Tag 列表
    private readonly ConcurrentDictionary<int, List<string>> _dependencyCache = new();

    public VirtualNodeEngine(
        ILogger<VirtualNodeEngine> logger,
        IServiceProvider serviceProvider,
        Func<string, object?>? getDataSnapshot = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
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
    /// 获取虚拟数据点的设备 ID
    /// </summary>
    public int GetDeviceId(int virtualDataPointId)
    {
        if (_virtualPointCache.TryGetValue(virtualDataPointId, out var point))
        {
            return point.DeviceId;
        }
        return 0;
    }

    /// <summary>
    /// 设置虚拟数据点列表（由外部加载后设置）
    /// </summary>
    public void SetVirtualPoints(IEnumerable<VirtualDataPoint> virtualPoints)
    {
        _virtualPointCache.Clear();
        _dependencyCache.Clear();

        foreach (var point in virtualPoints)
        {
            _virtualPointCache[point.Id] = point;

            // 预解析依赖，后续定时计算时只做取值和求值，减少重复正则扫描。
            // 构建依赖关系缓存
            var dependencies = ParseDependencies(point.Expression);
            _dependencyCache[point.Id] = dependencies;
        }

        _logger.LogInformation("虚拟数据点已设置：{Count} 个", virtualPoints.Count());
    }

    /// <summary>
    /// 刷新缓存并重新加载虚拟数据点
    /// </summary>
    public async Task RefreshCacheAsync()
    {
        _virtualPointCache.Clear();
        _dependencyCache.Clear();
        
        // 重新从数据库加载虚拟数据点
        await ReloadVirtualPointsAsync();
    }
    
    /// <summary>
    /// 从数据库重新加载虚拟数据点
    /// </summary>
    private async Task ReloadVirtualPointsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            var virtualPoints = await context.VirtualDataPoints
                .Include(p => p.Device)
                .Where(p => p.IsEnabled)
                .ToListAsync();

            foreach (var point in virtualPoints)
            {
                _virtualPointCache[point.Id] = point;
                var dependencies = ParseDependencies(point.Expression);
                _dependencyCache[point.Id] = dependencies;
            }

            _logger.LogInformation("虚拟数据点已重新加载：{Count} 个", virtualPoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新加载虚拟数据点失败");
        }
    }

    private VirtualNodeCalculationResult Calculate(VirtualDataPoint virtualDataPoint)
    {
        try
        {
            // 依赖值全部来自实时快照，因此虚拟点天然跟随采集结果和规则处理结果变化。
            // 从缓存获取依赖关系（避免重复解析表达式）
            var dependencies = _dependencyCache.GetOrAdd(virtualDataPoint.Id, _ => ParseDependencies(virtualDataPoint.Expression));
            var dependencyValues = new Dictionary<string, object?>();

            foreach (var depTag in dependencies)
            {
                // 从快照中获取最新值（通过委托）
                if (_getDataSnapshot != null)
                {
                    var value = _getDataSnapshot(depTag);
                    dependencyValues[depTag] = value;
                }
                else
                {
                    dependencyValues[depTag] = null;
                }
            }

            // 检查依赖数据是否完整，如果有依赖数据为空，跳过计算，直接将 Value 设为 null
            var missingDependencies = dependencies.Where(d => dependencyValues[d] == null).ToList();
            if (missingDependencies.Any() && dependencies.Any())
            {
                // 依赖不完整时返回不确定质量，避免用 0 或旧值误导后续发送链路。
                return new VirtualNodeCalculationResult
                {
                    Value = null,
                    Quality = DataQuality.Uncertain,
                    VirtualDataPointId = virtualDataPoint.Id,
                    VirtualDataPointTag = virtualDataPoint.Tag,
                    DependencyValues = dependencyValues
                };
            }

            var result = ExecuteCalculation(virtualDataPoint, dependencyValues);

            // 设置虚拟数据点 ID 和 Tag
            result.VirtualDataPointId = virtualDataPoint.Id;
            result.VirtualDataPointTag = virtualDataPoint.Tag;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟数据点 {Tag} 计算失败", virtualDataPoint.Tag);
            return VirtualNodeCalculationResult.Fail($"计算失败：{ex.Message}");
        }
    }

    public List<VirtualNodeCalculationResult> CalculateDevice(int deviceId)
    {
        if (!_virtualPointCache.Any())
        {
            _logger.LogWarning("虚拟数据点缓存为空");
            return new List<VirtualNodeCalculationResult>();
        }

        var results = new List<VirtualNodeCalculationResult>();

        foreach (var point in _virtualPointCache.Values)
        {
            if (point.DeviceId == deviceId)
            {
                var result = Calculate(point);
                results.Add(result);
            }
        }

        return results;
    }

    public List<VirtualNodeCalculationResult> CalculateAll()
    {
        if (!_virtualPointCache.Any())
        {
            _logger.LogWarning("虚拟数据点缓存为空");
            return new List<VirtualNodeCalculationResult>();
        }

        var results = new List<VirtualNodeCalculationResult>();

        foreach (var point in _virtualPointCache.Values)
        {
            var result = Calculate(point);
            results.Add(result);
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

        // 排除常见函数和关键字，剩余标识符默认视为数据点 Tag。
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

            // 四舍五入到 4 位小数
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
        return CalculateCustomExpression(expression, values);
    }

    private object? CalculateCustomExpression(string expression, Dictionary<string, object?> values)
    {
        if (string.IsNullOrEmpty(expression))
            return null;

        // 处理表达式：将带点号的 Tag 名用方括号包裹，使 NCalc 能正确识别
        var processedExpression = expression;

        // 按长度降序替换，避免 A 替换后误伤 A.B 这类更长的标签。
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
