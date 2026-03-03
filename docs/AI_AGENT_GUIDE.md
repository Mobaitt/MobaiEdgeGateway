# EdgeGateway AI Agent 开发指南

> 🤖 本文档专为 AI 助手设计，帮助 AI 快速理解项目结构并开发新功能

---

## 📖 如何使用本文档

### 给 AI 的提示词模板

当需要 AI 帮助开发功能时，使用以下模板：

```
请阅读 EdgeGateway 项目的 AI_AGENT_GUIDE.md 文档，然后帮我 [具体需求]。

项目位置：D:\Users\mobai\OneDrive\桌面\EdgeGateway

需求描述：
- 功能目标：[描述要实现的功能]
- 技术要点：[技术细节]
- 参考示例：[类似功能的文件路径]
```

---

## 🏗️ 项目架构速查

### 分层架构（重要！）

```
┌─────────────────────────────────────────┐
│  EdgeGateway.WebApi / EdgeGateway.Host  │  ← 程序入口
├─────────────────────────────────────────┤
│         EdgeGateway.Application         │  ← 业务编排
├─────────────────────────────────────────┤
│      EdgeGateway.Infrastructure         │  ← 技术实现
├─────────────────────────────────────────┤
│          EdgeGateway.Domain             │  ← 核心模型（最底层）
└─────────────────────────────────────────┘

依赖方向：WebApi → Application → Infrastructure → Domain
```

### 核心设计模式

**策略模式** 是本项目的核心设计模式：

```
接口定义（Domain/Interfaces/）
    ↓
策略注册器（Application/Services/）
    ↓
策略实现（Infrastructure/Strategies/）
```

---

## 📁 文件位置速查

### 添加采集协议

| 文件类型 | 路径 | 示例文件 |
|----------|------|----------|
| 枚举定义 | `Domain/Enums/CollectionProtocol.cs` | 添加新枚举值 |
| 策略接口 | `Domain/Interfaces/ICollectionStrategy.cs` | 查看接口定义 |
| 策略实现 | `Infrastructure/Strategies/Collection/` | `ModbusCollectionStrategy.cs` |
| 服务注册 | `Host/ServiceCollectionExtensions.cs` | 注册新策略 |

### 添加发送协议

| 文件类型 | 路径 | 示例文件 |
|----------|------|----------|
| 枚举定义 | `Domain/Enums/SendProtocol.cs` | 添加新枚举值 |
| 策略接口 | `Domain/Interfaces/ISendStrategy.cs` | 查看接口定义 |
| 策略实现 | `Infrastructure/Strategies/Send/` | `MqttSendStrategy.cs` |
| 服务注册 | `Host/ServiceCollectionExtensions.cs` | 注册新策略 |

### 添加规则类型

| 文件类型 | 路径 | 示例文件 |
|----------|------|----------|
| 枚举定义 | `Domain/Enums/RuleType.cs` | 添加新枚举值 |
| 规则配置 | `Domain/Entities/RuleConfigs.cs` | 添加配置类 |
| 规则引擎 | `Infrastructure/Rules/RuleEngine.cs` | 添加执行逻辑 |

### 添加虚拟节点计算类型

| 文件类型 | 路径 | 示例文件 |
|----------|------|----------|
| 枚举定义 | `Domain/Enums/CalculationType.cs` | 添加新枚举值 |
| 计算引擎 | `Infrastructure/VirtualNodes/VirtualNodeEngine.cs` | 添加计算逻辑 |

### 添加 API 接口

| 文件类型 | 路径 | 示例文件 |
|----------|------|----------|
| 请求 DTO | `WebApi/DTOs/Request/` | `CreateDeviceRequest.cs` |
| 响应 DTO | `WebApi/DTOs/Response/` | `DeviceResponse.cs` |
| 控制器 | `WebApi/Controllers/` | `DevicesController.cs` |

### 添加前端页面

| 文件类型 | 路径 | 示例文件 |
|----------|------|----------|
| API 调用 | `edge-gateway-ui/src/api/` | `device.ts` |
| 视图组件 | `edge-gateway-ui/src/views/` | `DevicesView.vue` |
| 路由配置 | `edge-gateway-ui/src/router/index.ts` | 添加路由 |

---

## 🎯 常见开发任务模板

### 任务 1：添加新的采集协议（如 OPC UA）

**提示词：**
```
请帮我添加 OPC UA 采集协议支持。

参考文件：
- Infrastructure/Strategies/Collection/ModbusCollectionStrategy.cs（参考实现）
- Domain/Enums/CollectionProtocol.cs（添加枚举）
- Host/ServiceCollectionExtensions.cs（注册服务）

实现要点：
1. 在 CollectionProtocol 枚举添加 OpcUa = 2
2. 创建 OpcUaCollectionStrategy.cs 实现 ICollectionStrategy 接口
3. 在 ServiceCollectionExtensions.cs 注册服务
4. 添加 NuGet 包：OPCFoundation.NetStandard.Opc.Ua
```

### 任务 2：添加新的发送协议（如 Kafka）

**提示词：**
```
请帮我添加 Kafka 发送协议支持。

参考文件：
- Infrastructure/Strategies/Send/MqttSendStrategy.cs（参考实现）
- Domain/Enums/SendProtocol.cs（添加枚举）
- Host/ServiceCollectionExtensions.cs（注册服务）

实现要点：
1. 在 SendProtocol 枚举添加 Kafka = 3
2. 创建 KafkaSendStrategy.cs 实现 ISendStrategy 接口
3. 在 ServiceCollectionExtensions.cs 注册服务
4. 添加 NuGet 包：Confluent.Kafka
```

### 任务 3：添加新的规则类型（如滤波规则）

**提示词：**
```
请帮我添加滤波规则类型。

参考文件：
- Domain/Enums/RuleType.cs（添加枚举）
- Domain/Entities/RuleConfigs.cs（添加配置类）
- Infrastructure/Rules/RuleEngine.cs（添加执行逻辑）

实现要点：
1. 在 RuleType 枚举添加 Filter = 4
2. 创建 FilterRuleConfig 类
3. 在 RuleEngine.cs 添加 ExecuteFilterRuleAsync 方法
```

### 任务 4：添加 API 接口

**提示词：**
```
请帮我添加设备批量导入 API。

参考文件：
- WebApi/DTOs/Request/CreateDeviceRequest.cs（参考请求 DTO）
- WebApi/DTOs/Response/DeviceResponse.cs（参考响应 DTO）
- WebApi/Controllers/DevicesController.cs（参考控制器）

实现要点：
1. 创建 ImportDevicesRequest DTO
2. 在 DevicesController.cs 添加 POST /api/devices/import 方法
3. 调用 DeviceManagementService 批量导入
```

### 任务 5：添加前端页面

**提示词：**
```
请帮我添加设备监控页面。

参考文件：
- edge-gateway-ui/src/views/DevicesView.vue（参考页面结构）
- edge-gateway-ui/src/api/device.ts（参考 API 调用）
- edge-gateway-ui/src/router/index.ts（添加路由）

实现要点：
1. 创建 MonitorView.vue 页面
2. 使用 WebSocket 订阅实时数据
3. 添加路由配置
```

---

## 📋 代码规范

### C# 命名规范

```csharp
// 类名：PascalCase
public class DataCollectionService { }

// 接口名：I 前缀 + PascalCase
public interface ICollectionStrategy { }

// 方法名：PascalCase
public async Task ConnectAsync() { }

// 私有字段：_camelCase
private readonly ILogger _logger;

// 属性名：PascalCase
public string DeviceName { get; set; }

// 局部变量：camelCase
var device = new Device();
```

### TypeScript/Vue 命名规范

```typescript
// 组件名：PascalCase
<template>
  <DeviceCard />
</template>

// 函数名：camelCase
const handleSave = async () => {}

// 变量名：camelCase
const deviceList = ref([])

// 类型名：PascalCase
interface DeviceResponse { }

// 文件名：PascalCase + 类型后缀
DevicesView.vue
device.ts
```

### 注释规范

```csharp
/// <summary>
/// 方法摘要（必填）
/// </summary>
/// <param name="device">参数说明（必填）</param>
/// <returns>返回值说明（必填）</returns>
public async Task ConnectAsync(Device device) { }
```

---

## 🔧 开发环境配置

### 后端

```bash
# .NET 版本检查
dotnet --version  # 需要 8.0+

# 还原依赖
dotnet restore

# 编译
dotnet build

# 运行
cd EdgeGateway.Host
dotnet run
```

### 前端

```bash
# Node.js 版本检查
node --version  # 需要 18+

# 安装依赖
cd edge-gateway-ui
npm install

# 开发模式
npm run dev

# 构建
npm run build
```

---

## 📚 核心概念解释

### 策略模式（Strategy Pattern）

**为什么使用策略模式？**
- 不同协议有不同的实现逻辑
- 需要动态切换协议
- 新增协议不影响现有代码

**策略模式三要素：**
1. **策略接口**（Domain/Interfaces/）- 定义统一方法
2. **具体策略**（Infrastructure/Strategies/）- 实现接口
3. **上下文**（Application/Services/）- 使用策略

### 仓储模式（Repository Pattern）

**为什么使用仓储模式？**
- 隔离业务逻辑和数据访问
- 便于单元测试
- 易于切换数据存储

### 依赖注入（Dependency Injection）

**服务生命周期：**
- `Singleton` - 单例（整个应用共享）
- `Scoped` - 作用域（每个请求一个实例）
- `Transient` - 瞬时（每次请求新实例）

**策略实现使用 Transient：**
```csharp
services.AddTransient<ModbusCollectionStrategy>();
```
原因：避免跨设备状态污染

---

## 🐛 常见问题排查

### 问题 1：编译错误 "找不到类型或命名空间"

**解决方案：**
1. 检查项目引用是否正确
2. 检查 using 语句是否完整
3. 运行 `dotnet restore`

### 问题 2：运行时错误 "策略未注册"

**解决方案：**
1. 检查 ServiceCollectionExtensions.cs 是否注册
2. 检查枚举值是否添加
3. 检查策略类是否实现接口

### 问题 3：前端 API 调用 404

**解决方案：**
1. 检查后端是否启动
2. 检查路由配置是否正确
3. 检查 CORS 配置

---

## 📖 推荐学习路径

### 第一次接触本项目？

1. **阅读 README.md** - 了解项目概况
2. **阅读 DEVELOPER_GUIDE.md** - 学习如何扩展
3. **阅读示例代码** - ModbusCollectionStrategy.cs
4. **尝试小改动** - 如添加日志
5. **实现新功能** - 如添加新协议

### 需要 AI 帮助开发？

1. **明确需求** - 写清楚要实现什么
2. **提供参考** - 指出参考文件
3. **指定位置** - 说明修改哪些文件
4. **验证结果** - 运行测试确保正确

---

## 📞 快速参考卡片

### 项目文件结构

```
EdgeGateway/
├── Domain/              # 核心模型（实体、枚举、接口）
├── Infrastructure/      # 技术实现（策略、仓储、引擎）
├── Application/         # 业务编排（应用服务）
├── Host/                # 控制台入口
├── WebApi/              # Web API 入口
└── edge-gateway-ui/     # Vue 3 前端
```

### 关键接口

```csharp
ICollectionStrategy    // 采集策略接口
ISendStrategy          // 发送策略接口
IRuleEngine            // 规则引擎接口
IVirtualNodeEngine     // 虚拟节点引擎接口
IDeviceRepository      // 设备仓储接口
```

### 关键服务

```csharp
DataCollectionService  // 数据采集
DataSendService        // 数据发送
DeviceManagementService // 设备管理
RuleManagementService  // 规则管理
VirtualNodeManagementService // 虚拟节点管理
```

### NuGet 包参考

| 功能 | 包名 |
|------|------|
| Modbus | NModbus4 |
| MQTT | MQTTnet |
| Kafka | Confluent.Kafka |
| OPC UA | OPCFoundation.NetStandard.Opc.Ua |
| S7 PLC | S7.Net |
| 表达式计算 | NCalcSync |

---

## 🤖 AI 开发最佳实践

### ✅ 推荐做法

1. **先理解再修改** - 阅读相关代码后再动手
2. **小步快跑** - 每次只改一点，及时验证
3. **参考现有代码** - 模仿已有实现的风格
4. **写注释** - 关键逻辑添加注释
5. **测试验证** - 运行确保功能正常

### ❌ 避免做法

1. **大规模重构** - 除非必要，不要改动太大
2. **破坏现有接口** - 保持向后兼容
3. **忽略错误处理** - 添加适当的异常处理
4. **硬编码** - 使用配置或常量
5. **不写文档** - 新功能要更新文档

---

## 📝 检查清单

### 添加新功能后检查

- [ ] 代码编译通过
- [ ] 运行无错误
- [ ] 添加了必要的注释
- [ ] 更新了相关文档
- [ ] 遵循了代码规范
- [ ] 添加了错误处理
- [ ] 测试了边界情况

---

## 🔗 相关文档

- [DEVELOPER_GUIDE.md](./DEVELOPER_GUIDE.md) - 详细开发者指南
- [README.md](../README.md) - 项目说明
- [ARCHITECTURE.md](./ARCHITECTURE.md) - 架构设计文档
