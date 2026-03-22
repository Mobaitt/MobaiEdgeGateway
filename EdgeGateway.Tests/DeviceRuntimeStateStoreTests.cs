using EdgeGateway.Application.Services;
using Xunit;

namespace EdgeGateway.Tests;

public class DeviceRuntimeStateStoreTests
{
    [Fact]
    public void GetSnapshot_ReturnsStopped_WhenDeviceHasNoState()
    {
        var store = new DeviceRuntimeStateStore();

        var snapshot = store.GetSnapshot(1);

        Assert.Equal(DeviceRuntimeStatus.Stopped, snapshot.Status);
        Assert.Equal("未运行", snapshot.StatusMessage);
        Assert.False(snapshot.IsConnected);
    }

    [Fact]
    public void GetOrAddState_ReusesExistingState_AndUpdatesDeviceName()
    {
        var store = new DeviceRuntimeStateStore();

        var first = store.GetOrAddState(10, "Pump-A");
        var second = store.GetOrAddState(10, "Pump-B");

        Assert.Same(first, second);
        Assert.Equal("Pump-B", second.DeviceName);
    }

    [Fact]
    public void RuntimeState_TracksFailureRateAndReconnectThreshold()
    {
        var state = RuntimeDeviceState.Create(1, "Boiler-01");

        state.MarkReadFailure("read-1", 1);
        state.MarkReadSuccess();
        state.MarkReadFailure("read-2", 1);
        state.MarkReadFailure("read-3", 2);

        Assert.Equal(75d, state.ReadFailureRatePercent);
        Assert.True(state.ShouldReconnectByFailureRate(4, 50));
        Assert.False(state.ShouldReconnectByFailureRate(5, 50));
    }

    [Fact]
    public void MarkAllStopped_UpdatesEveryTrackedDevice()
    {
        var store = new DeviceRuntimeStateStore();
        store.GetOrAddState(1, "A").MarkConnected();
        store.GetOrAddState(2, "B").MarkConnected();

        store.MarkAllStopped("全部停止");

        var snapshots = store.GetAllSnapshots();
        Assert.All(snapshots.Values, snapshot =>
        {
            Assert.Equal(DeviceRuntimeStatus.Stopped, snapshot.Status);
            Assert.Equal("全部停止", snapshot.StatusMessage);
        });
    }
}
