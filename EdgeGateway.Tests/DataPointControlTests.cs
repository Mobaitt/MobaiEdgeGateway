using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Strategies.Collection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EdgeGateway.Tests;

public class DataPointControlTests
{
    [Fact]
    public async Task Simulator_ControlledValueIsTypedAndVisibleToOtherConnections()
    {
        await using var context = await GatewayTestContext.CreateAsync();
        var point = await context.AddPointAsync();
        var control = context.Scope.ServiceProvider.GetRequiredService<DataPointControlService>();
        var value = await control.ControlAsync(point.DeviceId, point.Id, 123L);
        Assert.Equal((short)123, Assert.IsType<short>(value));

        var reader = context.Services.GetRequiredService<SimulatorCollectionStrategy>();
        await reader.ConnectAsync(point.Device);
        for (var i = 0; i < 30; i++)
        {
            CollectedData? result = null;
            await reader.ReadAsync([point], data => result = data);
            Assert.Equal((short)123, result!.Value);
            Assert.Equal(DataQuality.Good, result.Quality);
        }
        await reader.DisconnectAsync();
        Assert.Equal((short)123, Assert.Single(context.Collection.GetDeviceSnapshotData(point.DeviceId)).Value);
        await Assert.ThrowsAsync<OverflowException>(() => control.ControlAsync(point.DeviceId, point.Id, 100000));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FailedReadBack_ReplacesPreviousGoodSnapshotAndReportsUnconfirmedWrite(bool throws, bool uncertain)
    {
        var strategy = new FailedReadBackStrategy { ThrowOnRead = throws, Uncertain = uncertain };
        await using var context = await GatewayTestContext.CreateAsync(services => services.AddSingleton(strategy));
        var point = await context.AddPointAsync();
        context.Services.GetRequiredService<CollectionStrategyRegistry>().Register<FailedReadBackStrategy>(CollectionProtocol.Simulator);
        await context.Collection.OverrideDataPointValueAsync(point, (short)55, point.Device.Code);
        var control = context.Scope.ServiceProvider.GetRequiredService<DataPointControlService>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => control.ControlAsync(point.DeviceId, point.Id, 123));
        Assert.Contains("Write completed", error.Message);
        var snapshot = Assert.Single(context.Collection.GetDeviceSnapshotData(point.DeviceId));
        Assert.Equal(uncertain ? DataQuality.Uncertain : DataQuality.Bad, snapshot.Quality);
        if (!uncertain) Assert.Null(snapshot.Value);
        Assert.True(strategy.Written);
        Assert.True(strategy.Disconnected);
    }

    [Fact]
    public async Task RejectedSampleIsNullAndRejectedWithoutReplacingAcceptedSnapshot()
    {
        await using var context = await GatewayTestContext.CreateAsync();
        var point = await context.AddPointAsync();
        context.Db.DataPointRules.Add(new DataPointRule
        {
            DataPointIds = [point.Id],
            Name = "Range check",
            RuleType = RuleType.Validation,
            RuleConfig = "{\"ValidationType\":1,\"MinValue\":0,\"MaxValue\":100}",
            OnFailure = FailureAction.Reject
        });
        await context.Db.SaveChangesAsync();

        await context.Collection.OverrideDataPointValueAsync(point, (short)20, point.Device.Code);
        await context.Collection.OverrideDataPointValueAsync(point, (short)131, point.Device.Code);

        var accepted = Assert.Single(context.Collection.GetDeviceSnapshotData(point.DeviceId));
        Assert.Equal((short)20, accepted.Value);
        Assert.Equal(DataQuality.Good, accepted.Quality);

        var observed = Assert.Single(context.Collection.GetDeviceRealtimeData(point.DeviceId));
        Assert.Null(observed.Value);
        Assert.Equal(DataQuality.Rejected, observed.Quality);
    }

    public sealed class FailedReadBackStrategy : ICollectionStrategy
    {
        public bool ThrowOnRead { get; init; }
        public bool Uncertain { get; init; }
        public bool Written { get; private set; }
        public bool Disconnected { get; private set; }
        public string ProtocolName => "Simulator";
        public bool IsConnected => true;
        public Task ConnectAsync(Device device, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Disconnected = true;
            throw new IOException("Cleanup failure must not hide read-back failure");
        }
        public Task<object?> WriteAsync(DataPoint point, object? value, CancellationToken cancellationToken = default)
        {
            Written = true;
            return Task.FromResult(value);
        }
        public Task ReadAsync(IEnumerable<DataPoint> points, Action<CollectedData> callback, CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead) throw new IOException("Read failed");
            callback(new CollectedData { Value = Uncertain ? 12 : null, Quality = Uncertain ? DataQuality.Uncertain : DataQuality.Bad });
            return Task.CompletedTask;
        }
    }
}
