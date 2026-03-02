using System.Text.Json;
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>
/// 【发送策略实现】本地文件写入策略
/// 将采集数据追加写入本地 JSON 文件（每行一条 JSON Record，NDJSON 格式）
/// 适合离线场景、数据备份、或调试验证
///
/// 通道配置：
///   Endpoint: 输出文件路径，如 "./output/data.json"
///   ConfigJson: { "maxFileSizeMB": 100, "rotateDaily": true }
/// </summary>
public class LocalFileSendStrategy : ISendStrategy
{
    private readonly ILogger<LocalFileSendStrategy> _logger;
    private string _filePath = "./output/data.json";

    // 文件写入锁，防止并发写入损坏文件
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public LocalFileSendStrategy(ILogger<LocalFileSendStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProtocolName => "LocalFile";

    /// <inheritdoc/>
    public Task InitializeAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        _filePath = channel.Endpoint;

        // 确保输出目录存在
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("已创建输出目录：{Directory}", directory);
        }

        _logger.LogInformation("本地文件通道初始化完成，输出路径：{FilePath}", _filePath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 采用 NDJSON（Newline Delimited JSON）格式追加写入
    /// 每次发送作为一行 JSON 记录，方便按行解析和流式读取
    /// 包含所有数据点（即使质量为 Uncertain/Bad），确保数据结构完整
    /// </remarks>
    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // 构建数据行（支持别名映射）
            var aliasMap = package.Mappings
                .Where(m => m.IsEnabled)
                .ToDictionary(m => m.DataPointId, m => m.AliasName);

            // 构建统一格式的数据
            // 格式：{ "name": "DEV_SIMULATOR_001.DEV_SIMULATOR_001.Temperature", "value": 61.42, "unit": "℃", "quality": "Good" }
            var record = new
            {
                timestamp   = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                channelCode = package.Channel.Code,
                data = package.DataList
                    .Select(d => new
                    {
                        name = d.Tag,  // 使用完整 Tag（设备编码。数据点 Tag）
                        value = d.Value,
                        unit = d.Unit ?? string.Empty,
                        quality = d.Quality.ToString()
                    })
            };

            // 追加写入一行 JSON（NDJSON 格式）
            var line = JsonSerializer.Serialize(record);
            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, cancellationToken);

            _logger.LogDebug("文件写入成功 -> {FilePath}", _filePath);
            return SendResult.Success(package.DataList.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件写入失败，路径：{FilePath}", _filePath);
            return SendResult.Failure(ex.Message);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public Task DisposeAsync()
    {
        _writeLock.Dispose();
        return Task.CompletedTask;
    }
}
