using EdgeGateway.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// 纯控制台宿主入口
/// 不依赖 ASP.NET Core，适用于无需 Web API 的纯采集场景
/// 如需 Web API，请改用 EdgeGateway.WebApi 项目启动
/// </summary>
public static class ConsoleEntryPoint
{
    public static async Task RunAsync(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddEdgeGateway(context.Configuration, dbPath: "gateway.db");
                services.AddHostedService<GatewayWorker>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();

        await host.Services.InitializeDatabaseAsync();
        await host.RunAsync();
    }
}
