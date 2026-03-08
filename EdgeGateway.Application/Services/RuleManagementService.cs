using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EdgeGateway.Application.Services;

/// <summary>
/// 规则管理服务 - 负责数据点规则的 CRUD 和刷新缓存
/// </summary>
public class RuleManagementService
{
    private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;
    private readonly IRuleEngine _ruleEngine;
    private readonly ILogger<RuleManagementService> _logger;

    public RuleManagementService(
        IDbContextFactory<GatewayDbContext> dbContextFactory,
        IRuleEngine ruleEngine,
        ILogger<RuleManagementService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _ruleEngine = ruleEngine;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有规则
    /// </summary>
    public async Task<List<DataPointRule>> GetAllRulesAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.DataPointRules
            .Include(r => r.DataPoint)
            .Include(r => r.Device)
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }

    /// <summary>
    /// 根据 ID 获取规则
    /// </summary>
    public async Task<DataPointRule?> GetRuleByIdAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.DataPointRules
            .Include(r => r.DataPoint)
            .Include(r => r.Device)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>
    /// 获取指定数据点的所有规则
    /// </summary>
    public async Task<List<DataPointRule>> GetRulesByDataPointIdAsync(int dataPointId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.DataPointRules
            .Where(r => r.DataPointIdsJson != null && r.DataPointIds.Contains(dataPointId))
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }

    /// <summary>
    /// 获取指定设备的所有规则
    /// </summary>
    public async Task<List<DataPointRule>> GetRulesByDeviceIdAsync(int deviceId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.DataPointRules
            .Where(r => r.DeviceId == deviceId)
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }

    /// <summary>
    /// 获取全局规则
    /// </summary>
    public async Task<List<DataPointRule>> GetGlobalRulesAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.DataPointRules
            .Where(r => r.DataPointIds.Count == 0 && r.DeviceId == null)
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }

    /// <summary>
    /// 创建规则
    /// </summary>
    public async Task<DataPointRule> CreateRuleAsync(DataPointRule rule)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        // 验证 RuleConfig JSON 格式
        try
        {
            JsonConvert.DeserializeObject(rule.RuleConfig);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"RuleConfig JSON 格式无效：{ex.Message}", ex);
        }

        context.DataPointRules.Add(rule);
        await context.SaveChangesAsync();

        // 刷新规则引擎缓存
        await _ruleEngine.RefreshRulesCacheAsync();

        _logger.LogInformation("规则 [{RuleName}] 创建成功，ID={RuleId}", rule.Name, rule.Id);
        return rule;
    }

    /// <summary>
    /// 更新规则
    /// </summary>
    public async Task<DataPointRule> UpdateRuleAsync(DataPointRule rule)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var existing = await context.DataPointRules.FindAsync(rule.Id);
        if (existing == null)
            throw new InvalidOperationException($"规则 ID={rule.Id} 不存在");

        // 验证 RuleConfig JSON 格式
        try
        {
            JsonConvert.DeserializeObject(rule.RuleConfig);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"RuleConfig JSON 格式无效：{ex.Message}", ex);
        }

        // 记录更新前的数据点 ID 和设备 ID（用于清除缓存）
        var oldDataPointIds = existing.DataPointIds;
        var oldDeviceId = existing.DeviceId;

        existing.Name = rule.Name;
        existing.Description = rule.Description;
        existing.RuleType = rule.RuleType;
        existing.IsEnabled = rule.IsEnabled;
        existing.Priority = rule.Priority;
        existing.RuleConfig = rule.RuleConfig;
        existing.OnFailure = rule.OnFailure;
        existing.DefaultValueJson = rule.DefaultValueJson;
        existing.DataPointIds = rule.DataPointIds;
        existing.DeviceId = rule.DeviceId;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // 刷新规则引擎缓存
        await _ruleEngine.RefreshRulesCacheAsync();

        _logger.LogInformation("规则 [{RuleName}] 更新成功，ID={RuleId}", rule.Name, rule.Id);
        return existing;
    }

    /// <summary>
    /// 删除规则
    /// </summary>
    public async Task DeleteRuleAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var rule = await context.DataPointRules.FindAsync(id);
        if (rule == null)
            throw new InvalidOperationException($"规则 ID={id} 不存在");

        context.DataPointRules.Remove(rule);
        await context.SaveChangesAsync();

        // 刷新规则引擎缓存
        await _ruleEngine.RefreshRulesCacheAsync();

        _logger.LogInformation("规则 [{RuleName}] 删除成功，ID={RuleId}", rule.Name, rule.Id);
    }

    /// <summary>
    /// 启用/禁用规则
    /// </summary>
    public async Task ToggleRuleAsync(int id, bool isEnabled)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var rule = await context.DataPointRules.FindAsync(id);
        if (rule == null)
            throw new InvalidOperationException($"规则 ID={id} 不存在");

        rule.IsEnabled = isEnabled;
        rule.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // 刷新规则引擎缓存
        await _ruleEngine.RefreshRulesCacheAsync();

        _logger.LogInformation("规则 [{RuleName}] 已{Status}", rule.Name, isEnabled ? "启用" : "禁用");
    }

    /// <summary>
    /// 测试规则执行（不保存）
    /// </summary>
    public async Task<RuleExecutionResult> TestRuleAsync(DataPointRule rule, CollectedData testData)
    {
        return await _ruleEngine.ExecuteRulesAsync(testData);
    }
}
