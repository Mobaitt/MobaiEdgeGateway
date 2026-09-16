using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Enums;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EdgeGateway.Tests;

public class ChannelLifecycleTests
{
    [Fact]
    public async Task SwitchingToHttp_ReleasesOldStrategyAndPublishesAtNewEndpoint()
    {
        var recording = new RecordingStrategy();
        await using var context = await GatewayTestContext.CreateAsync(s => s.AddSingleton(recording));
        context.Services.GetRequiredService<SendStrategyRegistry>().Register<RecordingStrategy>(SendProtocol.Mqtt);
        var (channel, data) = await AddChannelAsync(context, SendProtocol.Mqtt);
        await context.Sender.InitializeChannelsAsync();
        await context.Sender.DispatchAsync([data]);
        Assert.Equal(1, recording.SendCount);

        channel.Protocol = SendProtocol.Http;
        channel.HttpMode = "server";
        channel.Endpoint = "updated";
        await context.Management.UpdateChannelAsync(channel);
        await context.Sender.DispatchAsync([data]);

        Assert.True(recording.Disposed);
        Assert.Equal(1, recording.SendCount);
        var response = await ReadEndpointAsync(context, "/api/http-data/updated");
        Assert.Equal(200, response.Status);
        Assert.Contains("test.value", response.Body);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("delete")]
    [InlineData("rename")]
    [InlineData("client")]
    public async Task ChangingHttpServer_RemovesOldEndpointAndCachedData(string action)
    {
        await using var context = await GatewayTestContext.CreateAsync();
        var (channel, data) = await AddChannelAsync(context, SendProtocol.Http);
        await context.Sender.InitializeChannelsAsync();
        await context.Sender.DispatchAsync([data]);
        Assert.Equal(200, (await ReadEndpointAsync(context, "/api/http-data/original")).Status);

        if (action == "delete") await context.Management.DeleteChannelAsync(channel.Id);
        else
        {
            if (action == "disable") channel.IsEnabled = false;
            if (action == "rename") channel.Endpoint = "new-path";
            if (action == "client") { channel.HttpMode = "client"; channel.Endpoint = "http://127.0.0.1:1"; }
            await context.Management.UpdateChannelAsync(channel);
        }
        // 客户端模式不实际请求外部地址；其他场景继续发送，确认旧缓存不会复活。
        if (action != "client") await context.Sender.DispatchAsync([data]);
        context.Services.GetRequiredService<HttpListenerService>().UpdateData("/api/http-data/original", "late data");
        var oldResponse = await ReadEndpointAsync(context, "/api/http-data/original");
        Assert.Equal(404, oldResponse.Status);
        Assert.Equal("Not Found", oldResponse.Body);
        if (action == "rename") Assert.Equal(200, (await ReadEndpointAsync(context, "/api/http-data/new-path")).Status);
    }

    [Fact]
    public async Task Disable_WaitsForActiveSendAndDoesNotRecreateStrategy()
    {
        var recording = new RecordingStrategy { BlockSend = true };
        await using var context = await GatewayTestContext.CreateAsync(s => s.AddSingleton(recording));
        context.Services.GetRequiredService<SendStrategyRegistry>().Register<RecordingStrategy>(SendProtocol.Mqtt);
        var (channel, data) = await AddChannelAsync(context, SendProtocol.Mqtt);
        await context.Sender.InitializeChannelsAsync();
        var sending = context.Sender.DispatchAsync([data]);
        await recording.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disabling = context.Management.DisableChannelAsync(channel.Id);
        try
        {
            Assert.False(disabling.IsCompleted);
            Assert.False(recording.Disposed);
        }
        finally { recording.Release.TrySetResult(); }
        await Task.WhenAll(sending, disabling).WaitAsync(TimeSpan.FromSeconds(5));
        await context.Sender.DispatchAsync([data]);
        Assert.True(recording.Disposed);
        Assert.Equal(1, recording.SendCount);
    }

    private static async Task<(Channel, CollectedData)> AddChannelAsync(GatewayTestContext context, SendProtocol protocol)
    {
        var point = await context.AddPointAsync();
        var channel = new Channel { Code = "test-channel", Name = "Test", Protocol = protocol, Endpoint = "original", HttpMode = "server" };
        context.Db.Channels.Add(channel);
        await context.Db.SaveChangesAsync();
        context.Db.ChannelDataPointMappings.Add(new ChannelDataPointMapping { ChannelId = channel.Id, DataPointId = point.Id });
        await context.Db.SaveChangesAsync();
        return (channel, new CollectedData { DataPointId = point.Id, Tag = point.Tag, Value = 42, Quality = DataQuality.Good });
    }

    private static async Task<(int Status, string Body)> ReadEndpointAsync(GatewayTestContext context, string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = path;
        using var stream = new MemoryStream();
        http.Response.Body = stream;
        await context.Services.GetRequiredService<HttpListenerService>().HandleRequestAsync(http);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return (http.Response.StatusCode, await reader.ReadToEndAsync());
    }

    public sealed class RecordingStrategy : ISendStrategy
    {
        public bool Disposed { get; private set; }
        public int SendCount { get; private set; }
        public bool BlockSend { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ProtocolName => "Mqtt";
        public Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
        {
            Assert.False(Disposed);
            Started.TrySetResult();
            if (BlockSend) await Release.Task.WaitAsync(cancellationToken);
            Assert.False(Disposed);
            SendCount++;
            return SendResult.Success(package.DataList.Count());
        }
        public Task DisposeAsync() { Disposed = true; return Task.CompletedTask; }
    }
}
