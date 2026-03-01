using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 发送通道仓储接口
/// 定义发送通道实体的持久化操作契约
/// </summary>
public interface IChannelRepository
{
    /// <summary>根据 ID 获取通道（含数据点映射导航属性）</summary>
    Task<Channel?> GetByIdAsync(int id);

    /// <summary>根据 ID 获取通道（无跟踪，用于获取原始值）</summary>
    Task<Channel?> GetByIdNoTrackingAsync(int id);

    /// <summary>获取所有通道</summary>
    Task<IEnumerable<Channel>> GetAllAsync();

    /// <summary>获取所有已启用的通道</summary>
    Task<IEnumerable<Channel>> GetEnabledAsync();

    /// <summary>
    /// 获取所有已启用的通道及其绑定的数据点映射（含数据点详情）
    /// 用于发送服务查询：该通道需要发送哪些数据点
    /// </summary>
    Task<IEnumerable<Channel>> GetEnabledWithMappingsAsync();

    /// <summary>新增发送通道</summary>
    Task<Channel> AddAsync(Channel channel);

    /// <summary>更新通道信息</summary>
    Task UpdateAsync(Channel channel);

    /// <summary>删除通道（级联删除其数据点映射关系）</summary>
    Task DeleteAsync(int id);
}
