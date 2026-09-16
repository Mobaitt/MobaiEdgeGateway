using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>通过 MQTT Broker 发布采集数据，每个通道持有独立连接。</summary>
public class MqttSendStrategy : ISendStrategy
{
    private readonly ILogger<MqttSendStrategy> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IMqttClient? _client;
    private MqttClientOptions? _options;
    private string _topic = "edge/data";
    private MqttQualityOfServiceLevel _qos;

    public MqttSendStrategy(ILogger<MqttSendStrategy> logger) => _logger = logger;
    public string ProtocolName => "Mqtt";

    public async Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        var endpoint = channel.Endpoint.Contains("://") ? channel.Endpoint : $"mqtt://{channel.Endpoint}";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "mqtt" && uri.Scheme != "mqtts") || string.IsNullOrWhiteSpace(uri.Host) ||
            uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("MQTT endpoint must be mqtt://host:port or mqtts://host:port.");

        var qos = channel.MqttQos ?? 0;
        if (qos is < 0 or > 2)
            throw new ArgumentException("MQTT QoS must be 0, 1 or 2.");
        var topic = string.IsNullOrWhiteSpace(channel.MqttTopic) ? "edge/data" : channel.MqttTopic;
        if (topic.IndexOfAny(['+', '#', '\0']) >= 0)
            throw new ArgumentException("MQTT publish topic cannot contain wildcards or null characters.");

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(uri.Host, uri.Port > 0 ? uri.Port : uri.Scheme == "mqtts" ? 8883 : 1883)
            .WithClientId(string.IsNullOrWhiteSpace(channel.MqttClientId)
                ? $"edge-{channel.Id}-{Guid.NewGuid():N}" : channel.MqttClientId)
            .WithTimeout(TimeSpan.FromSeconds(10));
        if (!string.IsNullOrEmpty(channel.MqttUsername))
            options.WithCredentials(channel.MqttUsername, channel.MqttPassword);
        if (uri.Scheme == "mqtts")
            options.WithTlsOptions(tls => tls.UseTls());

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _client?.Dispose();
            _client = new MqttFactory().CreateMqttClient();
            _options = options.Build();
            _topic = topic;
            _qos = (MqttQualityOfServiceLevel)qos;
            await _client.ConnectAsync(_options, cancellationToken);
            _logger.LogInformation("MQTT channel {ChannelName} connected", channel.Name);
        }
        catch
        {
            _client?.Dispose();
            _client = null;
            throw;
        }
        finally { _connectionLock.Release(); }
    }

    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_client == null || _options == null)
                return SendResult.Failure("MQTT channel is not initialized.");
            // 掉线后由下一轮发送重连，失败交给调度层记录。
            if (!_client.IsConnected)
                await _client.ConnectAsync(_options, cancellationToken);

            var data = package.DataList.ToList();
            var json = JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                gatewayId = "edge-gateway-001",
                data = data.Select(d => new
                {
                    name = d.Tag, value = d.Value, unit = d.Unit ?? string.Empty,
                    quality = d.Quality.ToString()
                })
            });
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_topic).WithPayload(json).WithQualityOfServiceLevel(_qos).Build();
            var result = await _client.PublishAsync(message, cancellationToken);
            if ((int)result.ReasonCode >= 128)
                return SendResult.Failure($"MQTT publish rejected: {result.ReasonCode}");
            return SendResult.Success(data.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT send failed for channel {Channel}", package.Channel.Name);
            return SendResult.Failure(ex.Message);
        }
        finally { _connectionLock.Release(); }
    }

    public async Task DisposeAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_client != null)
            {
                try
                {
                    if (_client.IsConnected)
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await _client.DisconnectAsync(new MqttClientDisconnectOptions(), timeout.Token);
                    }
                }
                finally { _client.Dispose(); _client = null; }
            }
        }
        finally { _connectionLock.Release(); }
    }
}
