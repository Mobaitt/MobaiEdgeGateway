using System.Collections.Concurrent;

namespace EdgeGateway.Application.Services;

public class DeviceRuntimeStateStore
{
    private readonly ConcurrentDictionary<int, RuntimeDeviceState> _deviceStates = new();

    public RuntimeDeviceSnapshot GetSnapshot(int deviceId)
    {
        if (_deviceStates.TryGetValue(deviceId, out var state))
            return state.ToSnapshot();

        return RuntimeDeviceSnapshot.Stopped();
    }

    public Dictionary<int, RuntimeDeviceSnapshot> GetAllSnapshots()
    {
        return _deviceStates.ToDictionary(pair => pair.Key, pair => pair.Value.ToSnapshot());
    }

    public RuntimeDeviceState GetOrAddState(int deviceId, string deviceName)
    {
        return _deviceStates.AddOrUpdate(
            deviceId,
            _ => RuntimeDeviceState.Create(deviceId, deviceName),
            (_, existing) =>
            {
                existing.DeviceName = deviceName;
                return existing;
            });
    }

    public void MarkStopped(int deviceId, string message)
    {
        if (_deviceStates.TryGetValue(deviceId, out var state))
            state.SetStatus(DeviceRuntimeStatus.Stopped, message);
    }

    public void MarkAllStopped(string message)
    {
        foreach (var state in _deviceStates.Values)
            state.SetStatus(DeviceRuntimeStatus.Stopped, message);
    }
}

public sealed class RuntimeDeviceSnapshot
{
    public string Status { get; init; } = DeviceRuntimeStatus.Stopped;
    public string StatusMessage { get; init; } = "未运行";
    public string? LastError { get; init; }
    public DateTime? LastConnectedAt { get; init; }
    public DateTime? LastReadAt { get; init; }
    public DateTime? LastFailureAt { get; init; }
    public DateTime StatusChangedAt { get; init; }
    public int ConsecutiveReadFailures { get; init; }
    public double ReadFailureRatePercent { get; init; }
    public int CurrentReconnectRound { get; init; }
    public int CurrentReconnectAttempt { get; init; }
    public bool IsConnected { get; init; }

    public static RuntimeDeviceSnapshot Stopped() => new()
    {
        Status = DeviceRuntimeStatus.Stopped,
        StatusMessage = "未运行",
        StatusChangedAt = DateTime.UtcNow
    };
}

public static class DeviceRuntimeStatus
{
    public const string Stopped = "stopped";
    public const string Running = "running";
    public const string Reconnecting = "reconnecting";
    public const string Warning = "warning";
    public const string Error = "error";
}

public sealed class RuntimeDeviceState
{
    private readonly object _syncRoot = new();
    private readonly Queue<bool> _readResults = new();

    public int DeviceId { get; }
    public string DeviceName { get; set; }
    public string Status { get; private set; } = DeviceRuntimeStatus.Stopped;
    public string StatusMessage { get; private set; } = "未运行";
    public string? LastError { get; private set; }
    public DateTime? LastConnectedAt { get; private set; }
    public DateTime? LastReadAt { get; private set; }
    public DateTime? LastFailureAt { get; private set; }
    public DateTime StatusChangedAt { get; private set; } = DateTime.UtcNow;
    public int ConsecutiveReadFailures { get; private set; }
    public double ReadFailureRatePercent { get; private set; }
    public int CurrentReconnectRound { get; set; }
    public int CurrentReconnectAttempt { get; set; }
    public bool IsConnected { get; private set; }

    private RuntimeDeviceState(int deviceId, string deviceName)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
    }

    public static RuntimeDeviceState Create(int deviceId, string deviceName) => new(deviceId, deviceName);

    public void SetStatus(string status, string message)
    {
        lock (_syncRoot)
        {
            Status = status;
            StatusMessage = message;
            StatusChangedAt = DateTime.UtcNow;
        }
    }

    public void MarkConnected()
    {
        lock (_syncRoot)
        {
            IsConnected = true;
            LastConnectedAt = DateTime.UtcNow;
            LastError = null;
            CurrentReconnectRound = 0;
            CurrentReconnectAttempt = 0;
            ConsecutiveReadFailures = 0;
            Status = DeviceRuntimeStatus.Running;
            StatusMessage = "设备运行中";
            StatusChangedAt = DateTime.UtcNow;
        }
    }

    public void MarkDisconnected()
    {
        lock (_syncRoot)
        {
            IsConnected = false;
        }
    }

    public void MarkConnectFailure(string error)
    {
        lock (_syncRoot)
        {
            IsConnected = false;
            LastError = error;
            LastFailureAt = DateTime.UtcNow;
            Status = DeviceRuntimeStatus.Reconnecting;
            StatusMessage = "连接失败，正在重连";
            StatusChangedAt = DateTime.UtcNow;
        }
    }

    public void MarkReadSuccess()
    {
        lock (_syncRoot)
        {
            IsConnected = true;
            LastReadAt = DateTime.UtcNow;
            LastError = null;
            ConsecutiveReadFailures = 0;
            Status = DeviceRuntimeStatus.Running;
            StatusMessage = "设备运行中";
            StatusChangedAt = DateTime.UtcNow;
            EnqueueReadResult(true);
        }
    }

    public void MarkReadFailure(string error, int consecutiveFailures)
    {
        lock (_syncRoot)
        {
            LastError = error;
            LastFailureAt = DateTime.UtcNow;
            ConsecutiveReadFailures = consecutiveFailures;
            Status = DeviceRuntimeStatus.Warning;
            StatusMessage = "读取异常";
            StatusChangedAt = DateTime.UtcNow;
            EnqueueReadResult(false);
        }
    }

    public void ResetReadFailureWindow()
    {
        lock (_syncRoot)
        {
            _readResults.Clear();
            ReadFailureRatePercent = 0;
            ConsecutiveReadFailures = 0;
        }
    }

    public bool ShouldReconnectByFailureRate(int windowSize, double thresholdPercent)
    {
        lock (_syncRoot)
        {
            if (_readResults.Count < windowSize)
                return false;

            return ReadFailureRatePercent >= thresholdPercent;
        }
    }

    public RuntimeDeviceSnapshot ToSnapshot()
    {
        lock (_syncRoot)
        {
            return new RuntimeDeviceSnapshot
            {
                Status = Status,
                StatusMessage = StatusMessage,
                LastError = LastError,
                LastConnectedAt = LastConnectedAt,
                LastReadAt = LastReadAt,
                LastFailureAt = LastFailureAt,
                StatusChangedAt = StatusChangedAt,
                ConsecutiveReadFailures = ConsecutiveReadFailures,
                ReadFailureRatePercent = ReadFailureRatePercent,
                CurrentReconnectRound = CurrentReconnectRound,
                CurrentReconnectAttempt = CurrentReconnectAttempt,
                IsConnected = IsConnected
            };
        }
    }

    private void EnqueueReadResult(bool success)
    {
        _readResults.Enqueue(success);
        while (_readResults.Count > 1000)
            _readResults.Dequeue();

        if (_readResults.Count == 0)
        {
            ReadFailureRatePercent = 0;
            return;
        }

        var failureCount = _readResults.Count(result => !result);
        ReadFailureRatePercent = failureCount * 100d / _readResults.Count;
    }
}
