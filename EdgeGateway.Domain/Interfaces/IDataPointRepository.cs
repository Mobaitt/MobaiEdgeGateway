using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 数据点仓储接口
/// 定义数据点实体的持久化操作契约
/// </summary>
public interface IDataPointRepository
{
    /// <summary>根据ID获取数据点</summary>
    Task<DataPoint?> GetByIdAsync(int id);

    /// <summary>获取指定设备下的所有数据点</summary>
    Task<IEnumerable<DataPoint>> GetByDeviceIdAsync(int deviceId);

    /// <summary>新增数据点</summary>
    Task<DataPoint> AddAsync(DataPoint dataPoint);

    /// <summary>更新数据点信息</summary>
    Task UpdateAsync(DataPoint dataPoint);

    /// <summary>删除数据点（级联删除相关映射关系）</summary>
    Task DeleteAsync(int id);
}
