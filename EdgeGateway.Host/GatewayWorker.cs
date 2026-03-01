using EdgeGateway.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Host;

/// <summary>
/// 控制台模式的网关后台工作服务
/// 在 Generic Host 生命周期内管理采集任务的启动与停止
/// </summary>
public class GatewayWorker : BackgroundService
{
    private readonly DataCollectionService _collectionService;
    private readonly DataSendService _sendService;
    private readonly ILogger<GatewayWorker> _logger;

    public GatewayWorker(
        DataCollectionService collectionService,
        DataSendService sendService,
        ILogger<GatewayWorker> logger)
    {
        _collectionService = collectionService;
        _sendService       = sendService;
        _logger            = logger;
    }

    /// <summary>初始化发送通道 → 启动所有设备采集循环</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("边缘采集网关启动（控制台模式）");

        await _sendService.InitializeChannelsAsync(stoppingToken);
        await _collectionService.StartAllAsync(stoppingToken);

        // 阻塞等待直到收到取消信号（Ctrl+C）
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>停止采集任务，释放发送资源</summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("网关正在关闭...");
        _collectionService.StopAll();
        await _collectionService.StopAggregatorAsync();
        await _sendService.DisposeAllAsync();
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("网关已安全退出");
    }
}
