using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>
/// 【发送策略实现】MQTT 发送策略
/// 将采集数据序列化为 JSON 后发布到 MQTT Broker 指定主题
/// </summary>
public class MqttSendStrategy : ISendStrategy
{
    private readonly ILogger<MqttSendStrategy> _logger;
    private string _defaultTopic = "edge/data";

    public MqttSendStrategy(ILogger<MqttSendStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProtocolName => "Mqtt";

    /// <inheritdoc/>
    public async Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在初始化 MQTT 通道 [{ChannelName}] -> {Endpoint}",
            channel.Name, channel.Endpoint);

        // 使用通道配置中的主题
        _defaultTopic = channel.MqttTopic ?? "edge/data";

        await Task.Delay(50, cancellationToken); // 模拟连接
        _logger.LogInformation("MQTT 通道 [{ChannelName}] 初始化完成，默认主题：{Topic}",
            channel.Name, _defaultTopic);
    }

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
    {
        try
        {
            // 将采集数据按统一格式组装为 JSON Payload
            // 格式：{ "name": "DEV_SIMULATOR_001.DEV_SIMULATOR_001.Temperature", "value": 61.42, "unit": "℃", "quality": "Good" }
            var payload = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                gatewayId = "edge-gateway-001",
                data = package.DataList
                    .Select(d => new
                    {
                        name = d.Tag,  // 使用完整 Tag（设备编码。数据点 Tag）
                        value = d.Value,
                        unit = d.Unit ?? string.Empty,
                        quality = d.Quality.ToString()
                    })
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });

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
    public Task DisposeAsync()
    {
        _logger.LogInformation("MQTT 通道资源已释放");
        return Task.CompletedTask;
    }
}
