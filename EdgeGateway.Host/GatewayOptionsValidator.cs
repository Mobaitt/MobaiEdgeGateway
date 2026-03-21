using EdgeGateway.Domain.Options;
using Microsoft.Extensions.Options;

namespace EdgeGateway.Host;

/// <summary>
/// 网关配置校验器
/// 在应用启动阶段对关键配置做边界检查，尽早暴露错误配置
/// </summary>
public sealed class GatewayOptionsValidator : IValidateOptions<GatewayOptions>
{
    private static readonly string[] SupportedInitializationModes = ["Auto", "Migrate", "EnsureCreated"];
    private static readonly string[] SupportedSeedModes = ["None", "Demo"];

    public ValidateOptionsResult Validate(string? name, GatewayOptions options)
    {
        var failures = new List<string>();

        if (options.Collection.AggregateWindowMs <= 0)
            failures.Add("Collection.AggregateWindowMs 必须大于 0。");

        if (options.Collection.DataExpirationSeconds <= 0)
            failures.Add("Collection.DataExpirationSeconds 必须大于 0。");

        if (options.Collection.MinPollingIntervalMs <= 0)
            failures.Add("Collection.MinPollingIntervalMs 必须大于 0。");

        if (options.Collection.MaxPollingIntervalMs < options.Collection.MinPollingIntervalMs)
            failures.Add("Collection.MaxPollingIntervalMs 不能小于 Collection.MinPollingIntervalMs。");

        if (options.Collection.DefaultPollingIntervalMs < options.Collection.MinPollingIntervalMs ||
            options.Collection.DefaultPollingIntervalMs > options.Collection.MaxPollingIntervalMs)
            failures.Add("Collection.DefaultPollingIntervalMs 必须位于 MinPollingIntervalMs 和 MaxPollingIntervalMs 之间。");

        if (options.Send.ChannelCacheExpirationSeconds <= 0)
            failures.Add("Send.ChannelCacheExpirationSeconds 必须大于 0。");

        if (options.Send.HttpTimeoutMs < 1000)
            failures.Add("Send.HttpTimeoutMs 不能小于 1000 毫秒。");

        if (options.Send.MqttQoS is < 0 or > 2)
            failures.Add("Send.MqttQoS 只能是 0、1 或 2。");

        if (options.Send.MaxConcurrentChannels <= 0)
            failures.Add("Send.MaxConcurrentChannels 必须大于 0。");

        if (options.Rules.CacheExpirationMinutes <= 0)
            failures.Add("Rules.CacheExpirationMinutes 必须大于 0。");

        if (options.VirtualNodes.CalculationCacheMs < 0)
            failures.Add("VirtualNodes.CalculationCacheMs 不能小于 0。");

        if (options.VirtualNodes.MaxConcurrentCalculations <= 0)
            failures.Add("VirtualNodes.MaxConcurrentCalculations 必须大于 0。");

        if (!string.Equals(options.Database.Type, "SQLite", StringComparison.OrdinalIgnoreCase))
            failures.Add("Database.Type 当前仅支持 SQLite。");

        if (string.IsNullOrWhiteSpace(options.Database.ConnectionString))
            failures.Add("Database.ConnectionString 不能为空。");

        if (!SupportedInitializationModes.Contains(options.Database.InitializationMode, StringComparer.OrdinalIgnoreCase))
            failures.Add($"Database.InitializationMode 仅支持：{string.Join(" / ", SupportedInitializationModes)}。");

        if (!SupportedSeedModes.Contains(options.Database.SeedMode, StringComparer.OrdinalIgnoreCase))
            failures.Add($"Database.SeedMode 仅支持：{string.Join(" / ", SupportedSeedModes)}。");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
