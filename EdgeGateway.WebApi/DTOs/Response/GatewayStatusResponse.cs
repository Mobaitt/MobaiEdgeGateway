namespace EdgeGateway.WebApi.DTOs.Response;

/// <summary>网关整体运行状态响应</summary>
public class GatewayStatusResponse
{
    /// <summary>网关服务是否正在运行（能访问到此接口即为运行中）</summary>
    public bool IsRunning { get; set; }

    /// <summary>总设备数</summary>
    public int TotalDevices { get; set; }

    /// <summary>已启用设备数</summary>
    public int EnabledDevices { get; set; }

    /// <summary>总发送通道数</summary>
    public int TotalChannels { get; set; }

    /// <summary>已启用通道数</summary>
    public int EnabledChannels { get; set; }

    /// <summary>总数据点数量</summary>
    public int TotalDataPoints { get; set; }

    /// <summary>服务器当前时间（UTC）</summary>
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;

    /// <summary>网关版本号</summary>
    public string Version { get; set; } = "1.0.0";
}
