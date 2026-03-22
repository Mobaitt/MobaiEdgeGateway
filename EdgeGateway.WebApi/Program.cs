using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Options;
using EdgeGateway.Host;
using EdgeGateway.Infrastructure.Http;
using EdgeGateway.Infrastructure.WebSocket;
using EdgeGateway.WebApi.Middleware;
using Microsoft.OpenApi.Models;

// =============================================
// 边缘采集网关 - Web API 入口
// =============================================
var builder = WebApplication.CreateBuilder(args);

// ========== 从配置文件读取网关配置 ==========
var gatewayOptions = builder.Configuration.GetSection("GatewayOptions").Get<GatewayOptions>() ?? new GatewayOptions();
builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("GatewayOptions"));

// 注册演示模式配置（从根目录读取）
builder.Services.Configure<DemoModeOptions>(builder.Configuration.GetSection("DemoMode"));

// 从配置获取数据库连接字符串
var dbPath = "gateway.db";
var connectionString = builder.Configuration["Database:ConnectionString"];
if (!string.IsNullOrEmpty(connectionString))
{
    // 解析连接字符串中的 Data Source
    var dsIndex = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
    if (dsIndex >= 0)
    {
        var dsPart = connectionString.Substring(dsIndex + "Data Source=".Length).Split(';')[0].Trim();
        if (!string.IsNullOrEmpty(dsPart))
            dbPath = dsPart;
    }
}

// ========== 注册网关核心服务（设备仓储、策略、采集/发送服务）==========
builder.Services.AddEdgeGateway(dbPath: dbPath);

// ========== Web API 相关服务 ==========
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        // 返回 JSON 使用驼峰命名
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

    // 读取 XML 注释（需要在 csproj 中启用 GenerateDocumentationFile）
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

// ========== 演示模式中间件（在 CORS 之前，拦截修改请求）==========
app.UseMiddleware<DemoModeMiddleware>();

// ========== CORS ==========
app.UseCors("AllowAll");

// ========== 启用静态文件服务（wwwroot）- 必须在 Swagger 和 Routing 之前 ==========
// 明确指定 wwwroot 目录
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot")) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot")) });

// ========== 启用 ASP.NET Core WebSocket 支持（必须在自定义 WebSocket 中间件之前） ==========
app.UseWebSockets();

// ========== WebSocket 服务端中间件（必须在 UseRouting 之前） ==========
app.UseWebSocketServer();

// ========== Swagger UI（在静态文件之后，Routing 之前）==========
app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/swagger/v1/swagger.json", "EdgeGateway API v1");
    opt.RoutePrefix = "swagger"; // Swagger 在 /swagger 路径
    opt.DocumentTitle = "边缘采集网关 API 文档";
});

app.UseRouting();

// HTTP 服务端模式数据端点中间件（必须在 MapControllers 之前）
// 拦截所有以 /api/http-data 开头的请求
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;

    // 这类请求不经过控制器，直接交给监听服务处理，便于实现设备主动上报模式。
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

// ========== SPA 回退路由（必须在 MapControllers 之后，处理 Vue Router 前端路由）==========
// 所有非 API 请求且不是静态文件的，都返回 index.html
// 排除：/api/*、/swagger/*、/ws
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"))
});

// 根路径重定向到 Swagger（可选）
app.MapGet("/api", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// =============================================
// 后台宿主服务（复用 Host 项目的实现）
// =============================================
/// <summary>
/// 网关后台宿主服务，在 Web Host 生命周期内并行运行采集任务
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
        _logger.LogInformation("网关采集服务启动（Web 模式）");
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
