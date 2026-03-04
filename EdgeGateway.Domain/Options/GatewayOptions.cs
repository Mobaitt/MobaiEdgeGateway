namespace EdgeGateway.Domain.Options;

/// <summary>
/// 网关配置选项
/// </summary>
public class GatewayOptions
{
    /// <summary>
    /// 采集配置
    /// </summary>
    public CollectionOptions Collection { get; set; } = new();

    /// <summary>
    /// 发送配置
    /// </summary>
    public SendOptions Send { get; set; } = new();

    /// <summary>
    /// 规则引擎配置
    /// </summary>
    public RulesOptions Rules { get; set; } = new();

    /// <summary>
    /// 虚拟节点配置
    /// </summary>
    public VirtualNodesOptions VirtualNodes { get; set; } = new();

    /// <summary>
    /// 数据库配置
    /// </summary>
    public DatabaseOptions Database { get; set; } = new();
}

/// <summary>
/// 采集配置选项
/// </summary>
public class CollectionOptions
{
    /// <summary>
    /// 数据聚合窗口间隔（毫秒）
    /// 默认值：1000ms（1 秒）
    /// 说明：采集数据后在此时间窗口内聚合，然后批量推送给发送服务
    /// </summary>
    public int AggregateWindowMs { get; set; } = 1000;

    /// <summary>
    /// 数据过期时间（秒）
    /// 默认值：30 秒
    /// 说明：超过此时间未更新的数据标记为 Uncertain 质量
    /// </summary>
    public int DataExpirationSeconds { get; set; } = 30;

    /// <summary>
    /// 默认采集周期（毫秒）
    /// 默认值：1000ms（1 秒）
    /// 说明：新建设备时的默认采集间隔
    /// </summary>
    public int DefaultPollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 最小采集周期（毫秒）
    /// 默认值：100ms
    /// 说明：防止采集过频影响系统性能
    /// </summary>
    public int MinPollingIntervalMs { get; set; } = 100;

    /// <summary>
    /// 最大采集周期（毫秒）
    /// 默认值：60000ms（60 秒）
    /// 说明：防止采集间隔过长导致数据不及时
    /// </summary>
    public int MaxPollingIntervalMs { get; set; } = 60000;
}

/// <summary>
/// 发送配置选项
/// </summary>
public class SendOptions
{
    /// <summary>
    /// 通道配置缓存过期时间（秒）
    /// 默认值：30 秒
    /// 说明：通道配置缓存在此时间后自动刷新
    /// </summary>
    public int ChannelCacheExpirationSeconds { get; set; } = 30;

    /// <summary>
    /// HTTP 发送超时时间（毫秒）
    /// 默认值：5000ms（5 秒）
    /// 说明：HTTP 请求超过此时间未响应则超时
    /// </summary>
    public int HttpTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// MQTT QoS 级别
    /// 默认值：1（至少一次）
    /// 说明：0=至多一次，1=至少一次，2=恰好一次
    /// </summary>
    public int MqttQoS { get; set; } = 1;

    /// <summary>
    /// 最大并发发送通道数
    /// 默认值：10
    /// 说明：限制并发发送数量，防止并发过高影响性能
    /// </summary>
    public int MaxConcurrentChannels { get; set; } = 10;
}

/// <summary>
/// 规则引擎配置选项
/// </summary>
public class RulesOptions
{
    /// <summary>
    /// 规则缓存过期时间（分钟）
    /// 默认值：5 分钟
    /// 说明：规则缓存在此时间后自动从数据库重新加载
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 5;
}

/// <summary>
/// 虚拟节点配置选项
/// </summary>
public class VirtualNodesOptions
{
    /// <summary>
    /// 虚拟节点计算结果缓存时间（毫秒）
    /// 默认值：500ms
    /// 说明：避免短时间内重复计算相同虚拟节点
    /// </summary>
    public int CalculationCacheMs { get; set; } = 500;

    /// <summary>
    /// 虚拟节点最大并发计算数
    /// 默认值：20
    /// 说明：限制并发计算数量，防止 CPU 占用过高
    /// </summary>
    public int MaxConcurrentCalculations { get; set; } = 20;
}

/// <summary>
/// 数据库配置选项
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// 数据库类型
    /// 默认值：SQLite
    /// 说明：目前仅支持 SQLite
    /// </summary>
    public string Type { get; set; } = "SQLite";

    /// <summary>
    /// 数据库连接字符串
    /// 默认值：Data Source=gateway.db
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=gateway.db";

    /// <summary>
    /// 是否启用敏感数据日志
    /// 默认值：false
    /// 说明：生产环境建议关闭，避免泄露敏感信息
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
