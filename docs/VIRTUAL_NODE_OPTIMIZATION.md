# 虚拟节点计算优化说明

## 修改日期
2026-03-05

## 优化内容

将以下两个 API 接口从"重新计算"改为"直接从快照获取数据"：

1. `POST /api/virtualnodes/points/calculate-all` - 获取所有虚拟数据点
2. `POST /api/virtualnodes/devices/{deviceId}/calculate` - 获取指定设备的虚拟数据点

## 修改原因

1. **性能优化**：虚拟数据点在数据采集过程中已经通过 `DataCollectionService` 实时计算并更新到快照中，无需重复计算
2. **数据一致性**：直接从快照获取数据，确保接口返回的值与发送通道推送的值完全一致
3. **减少资源消耗**：避免频繁的表达式解析和计算，降低 CPU 负载

## 优化详情

### VirtualNodeManagementService.cs

**简化计算方法**：
```csharp
// 优化前：30+ 行，包含冗余日志和空字典创建
public Task<VirtualNodeCalculationResult> CalculateVirtualDataPointAsync(int id)
{
    var snapshotData = _dataCollectionService.GetVirtualDataPointSnapshot(id);
    var result = snapshotData != null
        ? new VirtualNodeCalculationResult { ... }
        : new VirtualNodeCalculationResult { ... };
    return Task.FromResult(result);
}

// 优化后：表达式-bodied 方法
public Task<List<VirtualNodeCalculationResult>> CalculateDeviceVirtualDataPointsAsync(int deviceId) =>
    Task.FromResult(_dataCollectionService.GetDeviceVirtualDataPointSnapshots(deviceId));
```

**移除不必要的**：
- 冗余的 `DependencyValues` 初始化（前端未使用）
- 过多的日志输出
- 重复的 null 检查

### DataCollectionService.cs

**提取公共逻辑**：
```csharp
// 优化前：两个重复方法（40+ 行）
public List<VirtualNodeCalculationResult> GetDeviceVirtualDataPointSnapshots(int deviceId) { ... }
public List<VirtualNodeCalculationResult> GetAllVirtualDataPointSnapshots() { ... }

// 优化后：核心方法 + 委托
private List<VirtualNodeCalculationResult> GetVirtualDataPointSnapshotsCore(int? deviceId);
public List<VirtualNodeCalculationResult> GetDeviceVirtualDataPointSnapshots(int deviceId) =>
    GetVirtualDataPointSnapshotsCore(deviceId);
public List<VirtualNodeCalculationResult> GetAllVirtualDataPointSnapshots() =>
    GetVirtualDataPointSnapshotsCore(null);
```

**简化查询**：
```csharp
// 优化前
public List<CollectedData> GetDeviceSnapshotData(int deviceId)
{
    var results = new List<CollectedData>();
    foreach (var kvp in _dataSnapshot)
    {
        if (kvp.Value.Data.DeviceId != deviceId) continue;
        results.Add(kvp.Value.Data);
    }
    return results;
}

// 优化后
public List<CollectedData> GetDeviceSnapshotData(int deviceId) =>
    _dataSnapshot.Values.Where(x => x.Data.DeviceId == deviceId).Select(x => x.Data).ToList();
```

## 代码行数对比

| 文件 | 优化前 | 优化后 | 减少 |
|------|--------|--------|------|
| VirtualNodeManagementService.cs | 225 行 | 210 行 | -15 行 |
| DataCollectionService.cs | 898 行 | 858 行 | -40 行 |
| **合计** | **1123 行** | **1068 行** | **-55 行** |

## 性能提升

1. **减少对象分配**：移除不必要的 `Dictionary` 创建
2. **减少方法调用**：消除重复代码，提取公共逻辑
3. **简化条件判断**：使用模式匹配和表达式-bodied 方法
