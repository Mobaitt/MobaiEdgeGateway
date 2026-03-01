using EdgeGateway.Domain.Entities;

namespace EdgeGateway.Domain.Interfaces;

/// <summary>
/// 设备仓储接口
/// 定义设备实体的持久化操作契约
/// </summary>
public interface IDeviceRepository
{
    /// <summary>根据ID获取设备（含数据点导航属性）</summary>
    Task<Device?> GetByIdAsync(int id);

    /// <summary>获取所有设备（含数据点导航属性）</summary>
    Task<IEnumerable<Device>> GetAllAsync();

    /// <summary>获取所有已启用的设备（含已启用的数据点）</summary>
    Task<IEnumerable<Device>> GetEnabledAsync();

    /// <summary>新增设备</summary>
    Task<Device> AddAsync(Device device);

    /// <summary>更新设备信息</summary>
    Task UpdateAsync(Device device);

    /// <summary>删除设备（级联删除其数据点和映射关系）</summary>
    Task DeleteAsync(int id);
}
