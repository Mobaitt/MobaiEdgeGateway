using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 通道-数据点映射关系仓储接口
/// 定义 ChannelDataPointMapping 的持久化操作契约
/// </summary>
public interface IChannelMappingRepository
{
    /// <summary>获取指定通道的所有数据点映射（含数据点导航属性）</summary>
    Task<IEnumerable<ChannelDataPointMapping>> GetByChannelIdAsync(int channelId);

    /// <summary>获取指定数据点的所有通道映射（含通道导航属性）</summary>
    Task<IEnumerable<ChannelDataPointMapping>> GetByDataPointIdAsync(int dataPointId);

    /// <summary>新增单条映射关系</summary>
    Task<ChannelDataPointMapping> AddAsync(ChannelDataPointMapping mapping);

    /// <summary>
    /// 批量新增映射关系（绑定多个数据点到同一通道）
    /// </summary>
    Task AddRangeAsync(IEnumerable<ChannelDataPointMapping> mappings);

    /// <summary>删除映射关系</summary>
    Task DeleteAsync(int id);
}
