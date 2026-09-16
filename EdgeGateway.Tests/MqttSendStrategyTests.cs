using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Infrastructure.Strategies.Send;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Xunit;

namespace EdgeGateway.Tests;

public class MqttSendStrategyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Publish_ReachesRealSubscriberWithConfiguredTopicAndQos(int qos)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        var factory = new MqttFactory();
        using var server = factory.CreateMqttServer(new MqttServerOptionsBuilder()
            .WithDefaultEndpoint().WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
            .WithDefaultEndpointPort(port).Build());
        var credentials = new TaskCompletionSource<(string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ValidatingConnectionAsync += args =>
        {
            if (args.ClientId == "gateway-test") credentials.TrySetResult((args.UserName, args.Password));
            return Task.CompletedTask;
        };
        await server.StartAsync();
        using var subscriber = factory.CreateMqttClient();
        var received = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.ApplicationMessageReceivedAsync += args => { received.TrySetResult(args.ApplicationMessage); return Task.CompletedTask; };
        await subscriber.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build(), timeout.Token);
        await subscriber.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic("gateway/test").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)).Build(), timeout.Token);
        var strategy = new MqttSendStrategy(NullLogger<MqttSendStrategy>.Instance);
        try
        {
            var channel = new Channel
            {
                Endpoint = $"mqtt://127.0.0.1:{port}", MqttTopic = "gateway/test", MqttQos = qos,
                MqttClientId = "gateway-test", MqttUsername = "test-user", MqttPassword = "test-password"
            };
            await strategy.InitializeAsync(channel, timeout.Token);
            var result = await strategy.SendAsync(new SendPackage
            {
                Channel = channel,
                DataList = [new CollectedData { Tag = "test.value", Value = 42, Quality = DataQuality.Good }]
            }, timeout.Token);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(1, result.SentCount);
            var message = await received.Task.WaitAsync(timeout.Token);
            Assert.Equal("gateway/test", message.Topic);
            Assert.Equal((MqttQualityOfServiceLevel)qos, message.QualityOfServiceLevel);
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(message.PayloadSegment));
            Assert.Equal(42, json.RootElement.GetProperty("data")[0].GetProperty("value").GetInt32());
            Assert.Equal(("test-user", "test-password"), await credentials.Task.WaitAsync(timeout.Token));
            if (qos == 1)
            {
                // Broker 离线不能继续报成功；恢复后下一轮发送应自动重连。
                await server.StopAsync();
                var package = new SendPackage { Channel = channel, DataList = [new CollectedData { Tag = "reconnected", Value = 7 }] };
                Assert.False((await strategy.SendAsync(package, timeout.Token)).IsSuccess);
                await server.StartAsync();
                received = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                await subscriber.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build(), timeout.Token);
                await subscriber.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(filter => filter.WithTopic("gateway/test")).Build(), timeout.Token);
                var retry = await strategy.SendAsync(package, timeout.Token);
                Assert.True(retry.IsSuccess, retry.ErrorMessage);
                Assert.Contains("reconnected", Encoding.UTF8.GetString((await received.Task.WaitAsync(timeout.Token)).PayloadSegment));
            }
        }
        finally
        {
            await strategy.DisposeAsync();
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task SendWithoutConnection_DoesNotReportSuccess()
    {
        var strategy = new MqttSendStrategy(NullLogger<MqttSendStrategy>.Instance);
        try { Assert.False((await strategy.SendAsync(new SendPackage { Channel = new Channel() })).IsSuccess); }
        finally { await strategy.DisposeAsync(); }
    }

    [Theory]
    [InlineData("https://localhost", "test", 0)]
    [InlineData("mqtt://localhost", "test/+", 0)]
    [InlineData("mqtt://localhost", "test", 3)]
    public async Task InvalidConfiguration_IsRejected(string endpoint, string topic, int qos)
    {
        var strategy = new MqttSendStrategy(NullLogger<MqttSendStrategy>.Instance);
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => strategy.InitializeAsync(new Channel
            { Endpoint = endpoint, MqttTopic = topic, MqttQos = qos }));
        }
        finally { await strategy.DisposeAsync(); }
    }
}
