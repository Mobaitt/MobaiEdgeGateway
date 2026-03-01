using EdgeGateway.Application.Services;
using EdgeGateway.Host;
using EdgeGateway.Infrastructure.Http;
using EdgeGateway.Infrastructure.WebSocket;
using EdgeGateway.WebApi.Middleware;
using Microsoft.OpenApi.Models;

// =============================================
// 边缘采集网关 - Web API 入口
// =============================================
var builder = WebApplication.CreateBuilder(args);

// ========== 注册网关核心服务（设备仓储、策略、采集/发送服务）==========
builder.Services.AddEdgeGateway(dbPath: "gateway.db");

// ========== Web API 相关服务 ==========
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        // 返回JSON使用驼峰命名
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // 枚举序列化为字符串（方便前端展示）
        opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ========== Swagger / OpenAPI ==========
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "边缘采集网关 API",
        Version     = "v1",
        Description = "提供设备管理、数据点配置、发送通道管理及映射关系配置接口",
        Contact     = new OpenApiContact { Name = "EdgeGateway", Email = "admin@edge.local" }
    });

    // 读取XML注释（需要在csproj中启用 GenerateDocumentationFile）
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        opt.IncludeXmlComments(xmlPath);
});

// ========== CORS（开发环境允许所有来源）==========
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// ========== 后台采集服务 ==========
builder.Services.AddHostedService<GatewayHostedService>();

var app = builder.Build();

// ========== 初始化数据库 ==========
await app.Services.InitializeDatabaseAsync();

// ========== 全局异常中间件（最先注册）==========
app.UseMiddleware<GlobalExceptionMiddleware>();

// ========== Swagger UI（开发/生产都开放，可按需限制）==========
app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/swagger/v1/swagger.json", "EdgeGateway API v1");
    opt.RoutePrefix  = string.Empty; // 访问根路径直接打开Swagger
    opt.DocumentTitle = "边缘采集网关 API 文档";
});

app.UseCors("AllowAll");

// ========== 启用 ASP.NET Core WebSocket 支持（必须在自定义 WebSocket 中间件之前） ==========
app.UseWebSockets();

// ========== WebSocket 服务端中间件（必须在 UseRouting 之前） ==========
app.UseWebSocketServer();

app.UseRouting();

// HTTP 服务端模式数据端点中间件（必须在 MapControllers 之前）
// 拦截所有以 /api/http-data 开头的请求
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    
    // 检查是否是 HTTP 服务端模式的请求
    if (!string.IsNullOrEmpty(path) && path.StartsWith("/api/http-data", StringComparison.OrdinalIgnoreCase))
    {
        var httpListenerService = context.RequestServices.GetRequiredService<HttpListenerService>();
        await httpListenerService.HandleRequestAsync(context);
        return;
    }
    
    await next();
});

app.MapControllers();

// 根路径重定向到Swagger
app.MapGet("/api", () => Results.Redirect("/")).ExcludeFromDescription();

app.Run();

// =============================================
// 后台宿主服务（复用Host项目的实现）
// =============================================
/// <summary>
/// 网关后台宿主服务，在Web Host生命周期内并行运行采集任务
/// </summary>
public class GatewayHostedService : BackgroundService
{
    private readonly DataCollectionService _collectionService;
    private readonly DataSendService _sendService;
    private readonly ILogger<GatewayHostedService> _logger;

    public GatewayHostedService(
        DataCollectionService collectionService,
        DataSendService sendService,
        ILogger<GatewayHostedService> logger)
    {
        _collectionService = collectionService;
        _sendService       = sendService;
        _logger            = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("网关采集服务启动（Web模式）");
        await _sendService.InitializeChannelsAsync(stoppingToken);
        await _collectionService.StartAllAsync(stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("网关采集服务正在关闭...");
        _collectionService.StopAll();
        await _sendService.DisposeAllAsync();
        await base.StopAsync(cancellationToken);
    }
}
