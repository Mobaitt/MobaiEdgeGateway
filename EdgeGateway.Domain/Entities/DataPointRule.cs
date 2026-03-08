using System.Text.Json;
using EdgeGateway.Domain.Enums;

namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 数据点规则实体 - 定义数据点的采集限制、转换、校验规则
/// </summary>
public class DataPointRule
{
    /// <summary>主键</summary>
    public int Id { get; set; }

    /// <summary>所属数据点 ID 列表（外键，为 null 或空时表示不绑定特定数据点）</summary>
    /// <remarks>
    /// 支持多数据点关联，存储为 JSON 数组格式，如 "[1,2,3]"
    /// 如果只绑定一个数据点，存储为 "[1]"
    /// 如果不绑定数据点（全局规则或仅绑定设备），存储为 "[]" 或 null
    /// </remarks>
    public string? DataPointIdsJson { get; set; }

    /// <summary>所属设备 ID（为 null 时表示不绑定设备）</summary>
    public int? DeviceId { get; set; }

    /// <summary>规则名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规则描述</summary>
    public string? Description { get; set; }

    /// <summary>规则类型</summary>
    public RuleType RuleType { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>规则优先级（数值越小优先级越高，用于规则冲突时的处理）</summary>
    public int Priority { get; set; } = 100;

    /// <summary>规则配置（JSON 格式，根据 RuleType 不同而不同）</summary>
    public string RuleConfig { get; set; } = "{}";

    /// <summary>规则执行失败时的处理方式：Pass(放行)/Reject(拒绝)/DefaultValue(默认值)</summary>
    public FailureAction OnFailure { get; set; } = FailureAction.Pass;

    /// <summary>失败时的默认值（JSON 格式存储）</summary>
    public string? DefaultValueJson { get; set; }

    /// <summary>绑定的数据点 ID 列表（不映射到数据库，由 DataPointIdsJson 序列化/反序列化）</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<int> DataPointIds
    {
        get
        {
            if (string.IsNullOrEmpty(DataPointIdsJson))
                return new List<int>();
            
            try
            {
                return JsonSerializer.Deserialize<List<int>>(DataPointIdsJson) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }
        set => DataPointIdsJson = value != null && value.Count > 0 ? JsonSerializer.Serialize(value) : null;
    }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属数据点（导航属性，仅用于第一个数据点的关联）</summary>
    public DataPoint? DataPoint { get; set; }

    /// <summary>所属设备（导航属性）</summary>
    public Device? Device { get; set; }
}

/// <summary>
/// 规则执行失败时的处理方式
/// </summary>
public enum FailureAction
{
    /// <summary>
    /// 放行（记录警告日志，但不影响数据流）
    /// </summary>
    Pass = 0,

    /// <summary>
    /// 拒绝（丢弃该数据，不向下传递）
    /// </summary>
    Reject = 1,

    /// <summary>
    /// 使用默认值
    /// </summary>
    DefaultValue = 2
}
