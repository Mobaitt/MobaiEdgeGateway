using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>
/// 【发送策略实现】MQTT 发送策略
/// 将采集数据序列化为 JSON 后发布到 MQTT Broker 指定主题
///
/// 实际项目中可引入 MQTTnet NuGet 包实现，本文件提供完整框架
/// </summary>
public class MqttSendStrategy : ISendStrategy
{
    private readonly ILogger<MqttSendStrategy> _logger;

    // 实际项目中此处持有 MqttClient 实例
    // private IMqttClient? _mqttClient;
    private string _defaultTopic = "edge/data";

    public MqttSendStrategy(ILogger<MqttSendStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProtocolName => "Mqtt";

    /// <inheritdoc/>
    /// <remarks>
    /// 连接到 MQTT Broker
    /// 通道的 Endpoint 格式：mqtt://broker.example.com:1883
    /// 通道的 ConfigJson 可包含：{ "topic": "edge/factory1/data", "clientId": "...", "username": "...", "password": "..." }
    /// </remarks>
    public async Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在初始化 MQTT 通道 [{ChannelName}] -> {Endpoint}",
            channel.Name, channel.Endpoint);

        // 解析额外配置（主题等）
        if (!string.IsNullOrEmpty(channel.ConfigJson))
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(channel.ConfigJson);
            if (config != null && config.TryGetValue("topic", out var topic))
                _defaultTopic = topic;
        }

        // TODO: 引入 MQTTnet 后取消注释
        // var factory = new MqttFactory();
        // _mqttClient = factory.CreateMqttClient();
        // var options = new MqttClientOptionsBuilder()
        //     .WithTcpServer(host, port)
        //     .WithClientId(clientId)
        //     .Build();
        // await _mqttClient.ConnectAsync(options, cancellationToken);

        await Task.Delay(50, cancellationToken); // 模拟连接
        _logger.LogInformation("MQTT 通道 [{ChannelName}] 初始化完成，默认主题：{Topic}",
            channel.Name, _defaultTopic);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 将数据包序列化为 JSON，按映射关系（含别名）组装后发布到 MQTT 主题
    /// 包含所有数据点（即使质量为 Uncertain/Bad），确保数据结构完整
    /// </remarks>
    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
    {
        try
        {
            // 构建映射字典：DataPointId → AliasName（有别名用别名，没有用 Tag）
            var aliasMap = package.Mappings
                .Where(m => m.IsEnabled)
                .ToDictionary(m => m.DataPointId, m => m.AliasName);

            // 将采集数据按别名（或 Tag）组装为 JSON Payload（包含所有数据点）
            var payload = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["gatewayId"] = "edge-gateway-001",
                ["data"]      = package.DataList
                    .ToDictionary(
                        d => aliasMap.TryGetValue(d.DataPointId, out var alias) && !string.IsNullOrEmpty(alias)
                            ? alias : d.Tag,
                        d => d.Value
                    )
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });

            // TODO: 实际 MQTT 发布
            // var message = new MqttApplicationMessageBuilder()
            //     .WithTopic(_defaultTopic)
            //     .WithPayload(json)
            //     .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            //     .Build();
            // await _mqttClient!.PublishAsync(message, cancellationToken);

            _logger.LogDebug("MQTT 发送成功 -> 主题：{Topic}, Payload: {Json}", _defaultTopic, json);
            await Task.CompletedTask;

            return SendResult.Success(package.DataList.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT 发送失败，通道：{Channel}", package.Channel.Name);
            return SendResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        // await _mqttClient?.DisconnectAsync()!;
        // _mqttClient?.Dispose();
        await Task.CompletedTask;
        _logger.LogInformation("MQTT 通道资源已释放");
    }
}
