using System.Collections.Concurrent;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

/// <summary>模拟设备的寄存器状态，跨采集和控制连接共享，进程退出后清空。</summary>
public sealed class SimulatorValueStore
{
    private readonly ConcurrentDictionary<(int DeviceId, int DataPointId), object> _values = new();

    public void Write(int deviceId, int dataPointId, object value) => _values[(deviceId, dataPointId)] = value;
    public bool TryRead(int deviceId, int dataPointId, out object? value) =>
        _values.TryGetValue((deviceId, dataPointId), out value);
}
