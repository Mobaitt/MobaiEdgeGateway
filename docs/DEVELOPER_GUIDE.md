# EdgeGateway 开发者指南

> 详细的扩展开发指南，教你如何为 EdgeGateway 添加新功能

---

## 📋 目录

1. [项目结构概览](#项目结构概览)
2. [扩展采集协议](#扩展采集协议)
3. [扩展发送协议](#扩展发送协议)
4. [添加新的规则类型](#添加新的规则类型)
5. [添加虚拟节点计算类型](#添加虚拟节点计算类型)
6. [添加 API 接口](#添加 API 接口)
7. [前端组件开发](#前端组件开发)
8. [调试技巧](#调试技巧)

---

## 项目结构概览

### 分层架构

```
EdgeGateway/
├── Domain/              # 核心业务模型（实体、枚举、接口）
├── Infrastructure/      # 技术实现（策略、仓储、引擎）
├── Application/         # 业务编排（应用服务）
├── Host/                # 控制台宿主
├── WebApi/              # Web API 宿主
└── edge-gateway-ui/     # Vue 3 前端
```

### 关键文件位置

| 功能 | 文件路径 |
|------|----------|
| 采集策略接口 | `Domain/Interfaces/ICollectionStrategy.cs` |
| 发送策略接口 | `Domain/Interfaces/ISendStrategy.cs` |
| 采集策略实现 | `Infrastructure/Strategies/Collection/` |
| 发送策略实现 | `Infrastructure/Strategies/Send/` |
| 规则引擎 | `Infrastructure/Rules/RuleEngine.cs` |
| 虚拟节点引擎 | `Infrastructure/VirtualNodes/VirtualNodeEngine.cs` |
| 服务注册 | `Host/ServiceCollectionExtensions.cs` |

---

## 扩展采集协议

### 步骤 1：添加协议枚举

编辑 `Domain/Enums/CollectionProtocol.cs`：

```csharp
public enum CollectionProtocol
{
    Simulator = 0,    // 模拟器
    Modbus = 1,       // Modbus TCP/RTU
    OpcUa = 2,        // ⭐ 新增：OPC UA
    Virtual = 3,      // 虚拟设备
    S7 = 4            // ⭐ 新增：西门子 S7
}
```

### 步骤 2：创建策略实现类

在 `Infrastructure/Strategies/Collection/` 下创建 `OpcUaCollectionStrategy.cs`：

```csharp
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

/// <summary>
/// 【采集策略实现】OPC UA 采集策略
/// </summary>
public class OpcUaCollectionStrategy : ICollectionStrategy
{
    private readonly ILogger<OpcUaCollectionStrategy> _logger;
    // TODO: 添加 OPC UA 客户端字段
    
    public OpcUaCollectionStrategy(ILogger<OpcUaCollectionStrategy> logger)
    {
        _logger = logger;
    }

    public string ProtocolName => "OpcUa";

    public bool IsConnected => false; // TODO: 实现连接状态判断

    public async Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在连接 OPC UA 设备 [{DeviceName}] -> {Address}", 
            device.Name, device.Address);
        
        // TODO: 实现 OPC UA 连接逻辑
        await Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // TODO: 实现断开连接逻辑
        _logger.LogInformation("OPC UA 连接已断开");
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<CollectedData>> ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CollectedData>();
        var dataList = dataPoints.ToList();

        if (!dataList.Any())
            return results;

        var device = dataList.First().Device;
        var deviceCode = device?.Code ?? $"Device_{dataList.First().DeviceId}";

        // TODO: 实现 OPC UA 读取逻辑
        foreach (var dp in dataList)
        {
            results.Add(new CollectedData
            {
                Tag = $"{deviceCode}.{dp.Tag}",
                DataPointId = dp.Id,
                DeviceId = dp.DeviceId,
                DeviceName = deviceCode,
                Value = null, // TODO: 读取实际值
                Quality = DataQuality.Bad,
                Timestamp = DateTime.UtcNow
            });
        }

        return results;
    }
}
```

### 步骤 3：注册策略

编辑 `Host/ServiceCollectionExtensions.cs`：

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEdgeGateway(
        this IServiceCollection services,
        string dbPath = "gateway.db")
    {
        // ... 现有代码 ...
        
        // 采集策略注册
        services.AddTransient<SimulatorCollectionStrategy>();
        services.AddTransient<ModbusCollectionStrategy>();
        services.AddTransient<OpcUaCollectionStrategy>(); // ⭐ 新增
        
        return services;
    }

    public static IServiceCollection AddCollectionStrategies(
        this IServiceCollection services,
        CollectionStrategyRegistry registry)
    {
        registry.Register<SimulatorCollectionStrategy>(CollectionProtocol.Simulator);
        registry.Register<ModbusCollectionStrategy>(CollectionProtocol.Modbus);
        registry.Register<OpcUaCollectionStrategy>(CollectionProtocol.OpcUa); // ⭐ 新增
        
        return services;
    }
}
```

### 步骤 4：添加 NuGet 包

```bash
cd EdgeGateway.Infrastructure
dotnet add package OPCFoundation.NetStandard.Opc.Ua
```

---

## 扩展发送协议

### 步骤 1：添加协议枚举

编辑 `Domain/Enums/SendProtocol.cs`：

```csharp
public enum SendProtocol
{
    Mqtt = 1,         // MQTT
    Http = 2,         // HTTP
    Kafka = 3,        // ⭐ 新增：Kafka
    LocalFile = 4,    // 本地文件
    WebSocket = 5     // WebSocket
}
```

### 步骤 2：创建策略实现类

在 `Infrastructure/Strategies/Send/` 下创建 `KafkaSendStrategy.cs`：

```csharp
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdgeGateway.Infrastructure.Strategies.Send;

/// <summary>
/// 【发送策略实现】Kafka 发送策略
/// </summary>
public class KafkaSendStrategy : ISendStrategy
{
    private readonly ILogger<KafkaSendStrategy> _logger;
    // TODO: 添加 Kafka Producer 字段
    
    public KafkaSendStrategy(ILogger<KafkaSendStrategy> logger)
    {
        _logger = logger;
    }

    public string ProtocolName => "Kafka";

    public async Task InitializeAsync(Channel channel, CancellationToken cancellationToken)
    {
        _logger.LogInformation("初始化 Kafka 生产者 -> {Endpoint}", channel.Endpoint);
        
        // TODO: 初始化 Kafka Producer
        await Task.CompletedTask;
    }

    public async Task<SendResult> SendAsync(SendPackage package, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: 实现 Kafka 发送逻辑
            await Task.CompletedTask;
            
            return SendResult.Success($"已发送到 Kafka: {package.ChannelCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka 发送失败");
            return SendResult.Fail($"Kafka 发送失败：{ex.Message}");
        }
    }

    public Task DisposeAsync()
    {
        // TODO: 释放 Kafka Producer
        return Task.CompletedTask;
    }
}
```

### 步骤 3：注册策略

编辑 `Host/ServiceCollectionExtensions.cs`：

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSendStrategies(
        this IServiceCollection services,
        SendStrategyRegistry registry)
    {
        registry.Register<LocalFileSendStrategy>(SendProtocol.LocalFile);
        registry.Register<MqttSendStrategy>(SendProtocol.Mqtt);
        registry.Register<HttpSendStrategy>(SendProtocol.Http);
        registry.Register<KafkaSendStrategy>(SendProtocol.Kafka); // ⭐ 新增
        
        return services;
    }
}
```

### 步骤 4：添加 NuGet 包

```bash
cd EdgeGateway.Infrastructure
dotnet add package Confluent.Kafka
```

---

## 添加新的规则类型

### 步骤 1：添加规则类型枚举

编辑 `Domain/Enums/RuleType.cs`：

```csharp
public enum RuleType
{
    Limit = 0,         // 限制规则
    Transform = 1,     // 转换规则
    Validation = 2,    // 校验规则
    Calculation = 3,   // 计算规则
    Filter = 4         // ⭐ 新增：滤波规则
}
```

### 步骤 2：创建规则配置类

在 `Domain/Entities/` 下创建 `FilterRuleConfig.cs`：

```csharp
namespace EdgeGateway.Domain.Entities;

/// <summary>
/// 滤波规则配置
/// </summary>
public class FilterRuleConfig
{
    /// <summary>
    /// 滤波算法类型
    /// </summary>
    public FilterAlgorithm Algorithm { get; set; }
    
    /// <summary>
    /// 窗口大小（用于滑动平均）
    /// </summary>
    public int WindowSize { get; set; } = 5;
    
    /// <summary>
    /// 滤波系数（用于一阶滤波）
    /// </summary>
    public double Alpha { get; set; } = 0.5;
}

public enum FilterAlgorithm
{
    MovingAverage,    // 滑动平均
    FirstOrder,       // 一阶滤波
    Median            // 中值滤波
}
```

### 步骤 3：实现规则执行逻辑

编辑 `Infrastructure/Rules/RuleEngine.cs`，添加滤波规则处理：

```csharp
private async Task<RuleExecutionResult> ExecuteFilterRuleAsync(
    CollectedData data,
    DataPointRule rule,
    CancellationToken cancellationToken)
{
    var config = JsonConvert.DeserializeObject<FilterRuleConfig>(rule.RuleConfig);
    
    switch (config.Algorithm)
    {
        case FilterAlgorithm.MovingAverage:
            return await ApplyMovingAverageAsync(data, config);
        
        case FilterAlgorithm.FirstOrder:
            return await ApplyFirstOrderFilterAsync(data, config);
        
        case FilterAlgorithm.Median:
            return await ApplyMedianFilterAsync(data, config);
        
        default:
            return RuleExecutionResult.Fail($"未知的滤波算法：{config.Algorithm}");
    }
}

private async Task<RuleExecutionResult> ApplyMovingAverageAsync(
    CollectedData data,
    FilterRuleConfig config)
{
    // TODO: 实现滑动平均滤波
    // 需要从数据库或缓存中读取历史数据
    await Task.CompletedTask;
    return RuleExecutionResult.Success(data.Value);
}
```

---

## 添加虚拟节点计算类型

### 步骤 1：添加计算类型枚举

编辑 `Domain/Enums/CalculationType.cs`：

```csharp
public enum CalculationType
{
    Sum = 0,              // 求和
    Average = 1,          // 平均
    Max = 2,              // 最大值
    Min = 3,              // 最小值
    Count = 4,            // 计数
    WeightedAverage = 5,  // 加权平均
    Custom = 6,           // 自定义表达式
    StandardDeviation = 7 // ⭐ 新增：标准差
}
```

### 步骤 2：实现计算逻辑

编辑 `Infrastructure/VirtualNodes/VirtualNodeEngine.cs`：

```csharp
private async Task<VirtualNodeCalculationResult> CalculateByTypeAsync(
    VirtualDataPoint virtualPoint,
    Dictionary<string, object?> dependencyValues,
    CancellationToken cancellationToken)
{
    var values = dependencyValues.Values.Where(v => v != null).ToList();
    
    return virtualPoint.CalculationType switch
    {
        CalculationType.Sum => CalculateSum(values),
        CalculationType.Average => CalculateAverage(values),
        CalculationType.Max => CalculateMax(values),
        CalculationType.Min => CalculateMin(values),
        CalculationType.Count => CalculateCount(values),
        CalculationType.WeightedAverage => await CalculateWeightedAverageAsync(virtualPoint, dependencyValues),
        CalculationType.Custom => CalculateCustom(virtualPoint.Expression, dependencyValues),
        CalculationType.StandardDeviation => CalculateStandardDeviation(values), // ⭐ 新增
        _ => VirtualNodeCalculationResult.Fail("未知的计算类型")
    };
}

private VirtualNodeCalculationResult CalculateStandardDeviation(List<object?> values)
{
    if (values.Count < 2)
        return VirtualNodeCalculationResult.Fail("至少需要 2 个值");

    var numericValues = values.Select(v => Convert.ToDouble(v)).ToList();
    var avg = numericValues.Average();
    var variance = numericValues.Average(v => Math.Pow(v - avg, 2));
    var stdDev = Math.Sqrt(variance);

    return VirtualNodeCalculationResult.Success(stdDev);
}
```

---

## 添加 API 接口

### 步骤 1：创建 DTO

在 `WebApi/DTOs/Request/` 下创建 `TestRuleRequest.cs`：

```csharp
namespace EdgeGateway.WebApi.DTOs.Request;

public class TestRuleRequest
{
    /// <summary>
    /// 规则配置（JSON）
    /// </summary>
    public string RuleConfig { get; set; } = "{}";
    
    /// <summary>
    /// 测试输入值
    /// </summary>
    public object? InputValue { get; set; }
    
    /// <summary>
    /// 数据类型
    /// </summary>
    public DataValueType DataType { get; set; }
}
```

在 `WebApi/DTOs/Response/` 下创建 `TestRuleResponse.cs`：

```csharp
namespace EdgeGateway.WebApi.DTOs.Response;

public class TestRuleResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 输出值
    /// </summary>
    public object? OutputValue { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public double ExecutionTimeMs { get; set; }
}
```

### 步骤 2：创建 Controller

在 `WebApi/Controllers/` 下创建 `TestController.cs`：

```csharp
using EdgeGateway.Application.Services;
using EdgeGateway.Domain.Interfaces;
using EdgeGateway.WebApi.DTOs.Request;
using EdgeGateway.WebApi.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace EdgeGateway.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IRuleEngine _ruleEngine;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IRuleEngine ruleEngine,
        ILogger<TestController> logger)
    {
        _ruleEngine = ruleEngine;
        _logger = logger;
    }

    /// <summary>
    /// 测试规则
    /// </summary>
    [HttpPost("rule")]
    public async Task<ActionResult<ApiResponse<TestRuleResponse>>> TestRule(
        [FromBody] TestRuleRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var testData = new CollectedData
            {
                Tag = "test.tag",
                DataPointId = 0,
                DeviceId = 0,
                DeviceName = "Test",
                Value = request.InputValue,
                Quality = DataQuality.Good,
                Timestamp = DateTime.UtcNow
            };

            var result = await _ruleEngine.ExecuteRulesAsync(testData, HttpContext.RequestAborted);

            stopwatch.Stop();

            var response = new TestRuleResponse
            {
                Success = !result.ShouldReject,
                OutputValue = result.Value,
                ErrorMessage = result.ErrorMessage,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则测试失败");
            return BadRequest(ApiResponse<TestRuleResponse>.Fail($"规则测试失败：{ex.Message}"));
        }
    }
}
```

---

## 前端组件开发

### 步骤 1：创建 API 调用函数

在 `edge-gateway-ui/src/api/` 下创建 `test.ts`：

```typescript
import request from '@/utils/request'

export interface TestRuleRequest {
  ruleConfig: string
  inputValue: any
  dataType: number
}

export interface TestRuleResponse {
  success: boolean
  outputValue: any
  errorMessage: string | null
  executionTimeMs: number
}

/**
 * 测试规则
 */
export function testRule(data: TestRuleRequest) {
  return request<TestRuleResponse>({
    url: '/api/test/rule',
    method: 'post',
    data
  })
}
```

### 步骤 2：创建视图组件

在 `edge-gateway-ui/src/views/` 下创建 `TestView.vue`：

```vue
<template>
  <div class="test-view">
    <PageHeader title="规则测试" />
    
    <el-card class="test-card">
      <el-form :model="form" label-width="120px">
        <el-form-item label="输入值">
          <el-input-number v-model="form.inputValue" :precision="2" />
        </el-form-item>
        
        <el-form-item label="规则配置">
          <el-input
            v-model="form.ruleConfig"
            type="textarea"
            :rows="10"
            placeholder='{"type": "limit", "min": 0, "max": 100}'
          />
        </el-form-item>
        
        <el-form-item>
          <el-button type="primary" @click="handleTest" :loading="loading">
            测试
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>
    
    <el-card v-if="result" class="result-card">
      <h3>测试结果</h3>
      <el-descriptions :column="1">
        <el-descriptions-item label="输出值">{{ result.outputValue }}</el-descriptions-item>
        <el-descriptions-item label="执行时间">{{ result.executionTimeMs }}ms</el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="result.success ? 'success' : 'danger'">
            {{ result.success ? '成功' : '失败' }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item v-if="result.errorMessage" label="错误信息">
          {{ result.errorMessage }}
        </el-descriptions-item>
      </el-descriptions>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { testRule, type TestRuleRequest } from '@/api/test'

const form = reactive<TestRuleRequest>({
  ruleConfig: '{"type": "limit", "min": 0, "max": 100}',
  inputValue: 50,
  dataType: 4 // Int32
})

const loading = ref(false)
const result = ref<any>(null)

const handleTest = async () => {
  loading.value = true
  try {
    const res = await testRule(form)
    result.value = res
  } catch (error) {
    console.error('测试失败', error)
  } finally {
    loading.value = false
  }
}
</script>

<style scoped lang="scss">
.test-view {
  padding: 20px;
}

.test-card,
.result-card {
  margin-bottom: 20px;
}
</style>
```

### 步骤 3：添加路由

编辑 `edge-gateway-ui/src/router/index.ts`：

```typescript
const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    redirect: '/dashboard',
    children: [
      // ... 现有路由 ...
      {
        path: 'test',
        name: 'Test',
        component: () => import('@/views/TestView.vue'),
        meta: { title: '规则测试', icon: 'Experiment' }
      }
    ]
  }
]
```

---

## 调试技巧

### 后端调试

1. **启用详细日志**
   ```json
   // appsettings.json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug",
         "EdgeGateway": "Debug"
       }
     }
   }
   ```

2. **使用断点调试**
   ```bash
   # VS Code: 启动调试
   # Visual Studio: F5
   ```

3. **查看实时日志**
   ```bash
   dotnet run --verbosity detailed
   ```

### 前端调试

1. **启用 DevTools**
   ```typescript
   // vite.config.ts
   export default defineConfig({
     server: {
       proxy: {
         '/api': 'http://localhost:5000'
       }
     }
   })
   ```

2. **使用 Vue DevTools**
   - 安装 Vue DevTools 浏览器扩展
   - 查看组件树、状态、事件

3. **网络请求调试**
   - 浏览器 Network 面板
   - 查看 API 请求和响应

### 数据库调试

```bash
# 使用 SQLite 命令行工具
sqlite3 gateway.db

# 查看表
.tables

# 查询数据
SELECT * FROM Devices;
SELECT * FROM DataPoints;
```

---

## 📚 相关文档

- [AI_AGENT_GUIDE.md](./AI_AGENT_GUIDE.md) - AI Agent 开发指南
- [ARCHITECTURE.md](./ARCHITECTURE.md) - 架构设计文档
- [API_REFERENCE.md](./API_REFERENCE.md) - API 接口参考
