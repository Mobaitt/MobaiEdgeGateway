# EdgeGateway - 边缘计算数据采集网关

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Vue](https://img.shields.io/badge/Vue-3.4-4FC08D?logo=vue.js)](https://vuejs.org/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

> **边缘计算数据采集网关** - 基于策略模式架构，支持多种工业协议，易于扩展

---

## 📖 目录

1. [项目简介](#-项目简介)
2. [核心特性](#-核心特性)
3. [快速开始](#-快速开始)
4. [项目结构](#-项目结构)
5. [架构设计](#-架构设计)
6. [核心概念](#-核心概念)
7. [已支持协议](#-已支持协议)
8. [技术栈](#-技术栈)
9. [配置说明](#-配置说明)
10. [API 接口](#-api-接口)
11. [开发指南](#-开发指南)
12. [常见问题](#-常见问题)
13. [贡献指南](#-贡献指南)
14. [许可证](#-许可证)

---

## 📖 项目简介

EdgeGateway 是一款基于 **.NET 8** 和 **Vue 3** 开发的边缘计算数据采集网关。采用**策略模式 + 分层架构**设计，支持从工业设备（PLC、传感器等）采集数据，并通过多种方式（MQTT、HTTP、WebSocket 等）发送到云端或本地系统。

### 应用场景

- 🏭 **工业数据采集** - 从 PLC、传感器等设备采集生产数据
- 🏢 **楼宇自动化** - 采集空调、电力等系统数据
- 💧 **环境监测** - 采集水质、空气质量等监测数据
- 🔋 **能源管理** - 采集电力、水力等能耗数据
- 🚛 **设备监控** - 实时监控设备运行状态

### 核心价值

- **多协议采集** - 统一接口支持多种工业协议
- **灵活发送** - 支持多种数据发送方式
- **边缘计算** - 支持数据转换、计算、校验
- **易于扩展** - 策略模式设计，添加新协议简单
- **现代 UI** - 美观的管理界面，操作便捷

---

## ✨ 核心特性

### 数据采集
- 🔄 **多协议支持** - Modbus TCP/RTU、OPC UA、西门子 S7、模拟器
- ⏱️ **可配置周期** - 每个设备独立采集周期（100ms-60s）
- 📊 **实时快照** - 内存缓存最新数据，支持快速查询
- 🔌 **热插拔** - 设备配置变更无需重启服务

### 数据发送
- 📤 **MQTT** - 发布到 MQTT Broker，支持 QoS
- 🌐 **HTTP REST** - POST 到 HTTP 接口
- 🔗 **WebSocket** - 实时推送给订阅客户端
- 📁 **本地文件** - 写入 JSON/NDJSON 文件
- 🔄 **多通道并行** - 支持多个发送通道并行发送

### 边缘计算
- 🧮 **虚拟节点** - 支持表达式计算，创建派生数据点
- 📐 **规则引擎** - 限制、转换、校验、计算规则
- 🔀 **数据映射** - 灵活配置数据点与通道绑定关系
- 📈 **数据聚合** - 按窗口间隔批量推送，减少网络开销

### 系统特性
- 🎨 **现代 UI** - Vue 3 + TypeScript + Element Plus
- 💾 **轻量存储** - SQLite 数据库，便于部署
- 📝 **详细日志** - 分级日志，便于问题排查
- 🔒 **安全可靠** - 异常处理、数据过期、优雅关闭

---

## 🚀 快速开始

### 环境要求

| 软件 | 版本要求 | 说明 |
|------|----------|------|
| .NET SDK | 8.0+ | [下载地址](https://dotnet.microsoft.com/download) |
| Node.js | 18+ | [下载地址](https://nodejs.org/) |
| Git | 最新 | 用于克隆项目 |

### 方式一：直接运行（推荐新手）

```bash
# 1. 克隆项目
git clone https://github.com/your-org/EdgeGateway.git
cd EdgeGateway

# 2. 还原后端依赖并运行
dotnet restore
cd EdgeGateway.WebApi
dotnet run

# 3. 启动后自动完成：
#    - 创建 SQLite 数据库 gateway.db
#    - 写入种子数据（6 个模拟设备 + 45 个数据点 + 15 个虚拟节点）
#    - 开始采集数据（默认 2 秒/周期）
```

访问：
- **管理界面**: http://localhost:5000
- **API 文档**: http://localhost:5000/swagger
- **WebSocket**: ws://localhost:5000/ws

### 方式二：前后端分离开发

#### 后端启动

```bash
cd EdgeGateway.WebApi
dotnet run
```

#### 前端启动

```bash
cd edge-gateway-ui

# 安装依赖
npm install

# 开发模式（热重载）
npm run dev

# 构建生产版本
npm run build
```

访问前端：http://localhost:5173

### 方式三：使用控制台宿主

```bash
cd EdgeGateway.Host
dotnet run
```

此模式仅运行后端服务，无 Web 界面。

---

## 🏗️ 项目结构

### 整体目录

```
EdgeGateway/
├── EdgeGateway.Domain/              # 领域层（核心抽象，无外部依赖）
│   ├── Entities/                    # 实体类（Device, DataPoint, Channel 等）
│   ├── Enums/                       # 枚举定义（协议、数据类型等）
│   ├── Interfaces/                  # 核心接口（策略、仓储等）
│   └── Options/                     # 配置选项类
│
├── EdgeGateway.Infrastructure/      # 基础设施层（技术实现）
│   ├── Data/                        # EF Core DbContext
│   ├── Repositories/                # 仓储实现
│   ├── Strategies/                  # ★ 策略实现统一模块
│   │   ├── Collection/              # 采集策略（Modbus、Simulator 等）
│   │   └── Send/                    # 发送策略（MQTT、HTTP 等）
│   ├── Rules/                       # 规则引擎
│   ├── VirtualNodes/                # 虚拟节点引擎
│   ├── WebSocket/                   # WebSocket 服务
│   └── Http/                        # HTTP 客户端
│
├── EdgeGateway.Application/         # 应用层（业务编排）
│   └── Services/                    # 应用服务
│       ├── DataCollectionService.cs # 数据采集服务
│       ├── DataSendService.cs       # 数据发送服务
│       ├── DeviceManagementService.cs # 设备管理服务
│       ├── RuleManagementService.cs # 规则管理服务
│       ├── VirtualNodeManagementService.cs # 虚拟节点服务
│       ├── CollectionStrategyRegistry.cs # 采集策略注册器
│       └── SendStrategyRegistry.cs  # 发送策略注册器
│
├── EdgeGateway.Host/                # 控制台宿主（后台服务）
│   ├── Program.cs                   # 入口文件
│   ├── GatewayWorker.cs             # 后台工作服务
│   ├── ServiceCollectionExtensions.cs # 服务注册扩展
│   └── DatabaseSeeder.cs            # 数据库种子数据
│
├── EdgeGateway.WebApi/              # Web API 宿主
│   ├── Program.cs                   # 入口文件
│   ├── Controllers/                 # API 控制器
│   ├── DTOs/                        # 数据传输对象
│   └── Middleware/                  # 中间件
│
├── edge-gateway-ui/                 # Vue 3 前端项目
│   ├── src/
│   │   ├── api/                     # API 调用
│   │   ├── views/                   # 页面组件
│   │   ├── components/              # 通用组件
│   │   ├── router/                  # 路由配置
│   │   └── stores/                  # 状态管理
│   └── package.json
│
└── docs/                            # 文档目录
    ├── ARCHITECTURE.md              # 架构设计文档
    ├── DEVELOPER_GUIDE.md           # 开发者指南
    ├── AI_AGENT_GUIDE.md            # AI Agent 开发指南
    ├── API_REFERENCE.md             # API 参考文档
    └── DATABASE_RELATIONSHIPS.md    # 数据库关系说明
```

### 依赖关系图

```
┌─────────────────────────────────────────┐
│     EdgeGateway.WebApi / Host           │  ← 程序入口
├─────────────────────────────────────────┤
│         EdgeGateway.Application         │  ← 业务编排
├─────────────────────────────────────────┤
│      EdgeGateway.Infrastructure         │  ← 技术实现
├─────────────────────────────────────────┤
│          EdgeGateway.Domain             │  ← 核心模型（最底层）
└─────────────────────────────────────────┘

依赖方向：WebApi → Application → Infrastructure → Domain
```

---

## 🎯 架构设计

### 策略模式（核心设计）

EdgeGateway 的核心设计模式是**策略模式**，用于支持多种采集协议和发送方式。

#### 采集策略

```
┌─────────────┐
│   Device    │─── 协议类型 ───► CollectionStrategyRegistry
└─────────────┘                       │
                                      ├──► SimulatorStrategy
                                      ├──► ModbusStrategy
                                      ├──► OpcUaStrategy (待扩展)
                                      └──► S7Strategy (待扩展)
```

#### 发送策略

```
采集数据 ──► DataSendService ──► 按通道映射分发
                                      │
                                      ├──► MqttSendStrategy
                                      ├──► HttpSendStrategy
                                      ├──► WebSocketSendStrategy
                                      └──► LocalFileSendStrategy
```

### 数据流

#### 采集数据流

```
设备 (PLC/传感器)
    ↓
ICollectionStrategy.ReadAsync()
    ↓
CollectedData[] (原始数据)
    ↓
RuleEngine.ExecuteRulesAsync()
    ↓
CollectedData[] (处理后数据)
    ↓
DataCollectionService.UpdateSnapshotAsync()
    ↓
[数据快照缓存]
    ↓
DataSendService.DispatchAsync()
    ↓
ISendStrategy.SendAsync()
    ↓
目标系统 (MQTT/HTTP/文件等)
```

#### 虚拟节点计算流

```
DataPoint.Tag 更新
    ↓
VirtualNodeEngine.OnDependencyDataUpdatedAsync()
    ↓
解析依赖关系
    ↓
NCalc 执行表达式
    ↓
VirtualDataPoint 更新
    ↓
[触发新的虚拟节点计算] (递归)
    ↓
VirtualNodeCalculationResult[]
    ↓
DataCollectionService.UpdateSnapshotAsync()
```

### 模块关系

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                    │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐  │
│  │  Web API    │  │  Swagger    │  │  Vue Frontend   │  │
│  └─────────────┘  └─────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────┘
                            │
┌───────────────────────────▼───────────────────────────────┐
│                    Application Layer                       │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Collection   │  │    Send      │  │   Management    │  │
│  │  Service     │  │   Service    │  │    Services     │  │
│  └──────────────┘  └──────────────┘  └─────────────────┘  │
└───────────────────────────────────────────────────────────┘
                            │
┌───────────────────────────▼───────────────────────────────┐
│                  Infrastructure Layer                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │Strategy  │  │Repository│  │   Rule   │  │ Virtual  │  │
│  │          │  │          │  │  Engine  │  │  Engine  │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
└───────────────────────────────────────────────────────────┘
                            │
┌───────────────────────────▼───────────────────────────────┐
│                     Domain Layer                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │ Entities │  │  Enums   │  │Interfaces│  │ Options  │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
└───────────────────────────────────────────────────────────┘
```

---

## 📋 核心概念

### 设备（Device）

代表一个现场采集设备（如 PLC、传感器），包含：

| 属性 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| Name | string | 设备名称 |
| Code | string | 设备编码（唯一） |
| Protocol | CollectionProtocol | 协议类型 |
| Address | string | IP 地址或串口号 |
| Port | int? | 通信端口 |
| IsEnabled | bool | 是否启用 |
| PollingIntervalMs | int | 采集周期（毫秒） |

### 数据点（DataPoint）

设备上的一个监测点，包含：

| 属性 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| DeviceId | int | 所属设备 ID |
| Tag | string | 全局唯一标识 |
| Name | string | 数据点名称 |
| Address | string | 寄存器地址或 OPC 节点 |
| DataType | DataValueType | 数据类型 |
| Unit | string? | 工程量单位 |
| ModbusSlaveId | byte? | Modbus 从站地址 |
| ModbusFunctionCode | int? | Modbus 功能码 |

### 虚拟数据点（VirtualDataPoint）

基于表达式计算的派生数据点：

| 属性 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| DeviceId | int | 所属设备 ID |
| Tag | string | 全局唯一标识 |
| Expression | string | 计算表达式 |
| CalculationType | CalculationType | 计算类型 |
| DependencyTags | string | 依赖的 Tags（JSON） |

### 发送通道（Channel）

数据发送的目标，包含：

| 属性 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| Name | string | 通道名称 |
| Code | string | 通道编码（唯一） |
| Protocol | SendProtocol | 发送协议类型 |
| Endpoint | string | 连接字符串 |
| MqttTopic | string? | MQTT 主题 |
| IsEnabled | bool | 是否启用 |

### 规则（DataPointRule）

数据处理规则：

| 属性 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| Name | string | 规则名称 |
| RuleType | RuleType | 规则类型 |
| DataPointIdsJson | string | 应用的数据点 ID 列表（JSON） |
| Priority | int | 优先级 |
| RuleConfig | string | 规则配置（JSON） |
| OnFailure | RuleFailureAction | 失败处理动作 |

### 数据映射（ChannelDataPointMapping）

通道与数据点的绑定关系：

| 属性 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| ChannelId | int | 通道 ID |
| DataPointId | int? | 普通数据点 ID |
| VirtualDataPointId | int? | 虚拟数据点 ID |
| DataPointTag | string | 数据点 Tag 快照 |

---

## 📦 已支持的协议

### 采集协议

| 协议 | 状态 | 说明 | NuGet 包 |
|------|------|------|----------|
| Simulator | ✅ 已支持 | 模拟器（生成随机数据） | - |
| Modbus TCP | ✅ 已支持 | Modbus TCP 协议 | NModbus4 |
| Modbus RTU | ✅ 已支持 | Modbus RTU 协议（串口） | NModbus4 + System.IO.Ports |
| OPC UA | 🔲 待扩展 | OPC UA 协议 | OPCFoundation.NetStandard.Opc.Ua |
| 西门子 S7 | 🔲 待扩展 | 西门子 PLC 协议 | S7.Net |

### 发送协议

| 协议 | 状态 | 说明 | NuGet 包 |
|------|------|------|----------|
| MQTT | ✅ 已支持 | 发布到 MQTT Broker | MQTTnet |
| HTTP REST | ✅ 已支持 | POST 到 HTTP 接口 | - |
| WebSocket | ✅ 已支持 | 推送给订阅客户端 | - |
| 本地文件 | ✅ 已支持 | 写入 JSON/NDJSON 文件 | - |
| Kafka | 🔲 待扩展 | 发送到 Kafka 集群 | Confluent.Kafka |

---

## 🛠️ 技术栈

### 后端

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | 主框架 |
| Entity Framework Core | 8.0 | ORM 框架 |
| SQLite | - | 嵌入式数据库 |
| NModbus4 | 2.1.0 | Modbus 协议库 |
| MQTTnet | - | MQTT 客户端库 |
| NCalcSync | 5.4.2 | 表达式计算引擎 |
| Newtonsoft.Json | 13.0.3 | JSON 序列化 |
| System.IO.Ports | 8.0.0 | 串口通信 |

### 前端

| 技术 | 版本 | 用途 |
|------|------|------|
| Vue | 3.4 | 前端框架 |
| TypeScript | 5.6 | 类型系统 |
| Vite | 5.1 | 构建工具 |
| Element Plus | 2.6 | UI 组件库 |
| Pinia | 2.1 | 状态管理 |
| Vue Router | 4.3 | 路由管理 |
| Axios | 1.6 | HTTP 客户端 |
| pinyin-pro | 3.28 | 拼音转换 |

---

## ⚙️ 配置说明

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=gateway.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "EdgeGateway": "Information"
    }
  },
  "AllowedHosts": "*",
  "GatewayOptions": {
    "Collection": {
      "AggregateWindowMs": 1000,
      "DataExpirationSeconds": 30,
      "DefaultPollingIntervalMs": 1000,
      "MinPollingIntervalMs": 100,
      "MaxPollingIntervalMs": 60000
    },
    "Send": {
      "ChannelCacheExpirationSeconds": 30,
      "HttpTimeoutMs": 5000,
      "MqttQoS": 1,
      "MaxConcurrentChannels": 10
    },
    "Rules": {
      "CacheExpirationMinutes": 5
    },
    "VirtualNodes": {
      "CalculationCacheMs": 500,
      "MaxConcurrentCalculations": 20
    }
  },
  "DemoMode": {
    "Enabled": false
  }
}
```

### 配置项说明

#### 采集配置（Collection）

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| AggregateWindowMs | 1000 | 数据聚合窗口（毫秒） |
| DataExpirationSeconds | 30 | 数据过期时间（秒） |
| DefaultPollingIntervalMs | 1000 | 默认采集周期（毫秒） |
| MinPollingIntervalMs | 100 | 最小采集周期（毫秒） |
| MaxPollingIntervalMs | 60000 | 最大采集周期（毫秒） |

#### 发送配置（Send）

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| ChannelCacheExpirationSeconds | 30 | 通道配置缓存过期时间（秒） |
| HttpTimeoutMs | 5000 | HTTP 请求超时（毫秒） |
| MqttQoS | 1 | MQTT 服务质量等级 |
| MaxConcurrentChannels | 10 | 最大并发发送通道数 |

### 数据库迁移

```bash
# 添加迁移
dotnet ef migrations add InitialCreate --project EdgeGateway.Infrastructure

# 更新数据库
dotnet ef database update --project EdgeGateway.Host

# 删除迁移
dotnet ef migrations remove --project EdgeGateway.Infrastructure
```

---

## 🌐 API 接口

### 基础信息

- **基础 URL**: `http://localhost:5000/api`
- **API 文档**: `http://localhost:5000/swagger`
- **认证方式**: 当前版本无需认证

### 主要接口

| 模块 | 接口 | 说明 |
|------|------|------|
| 设备管理 | `GET/POST/PUT/DELETE /api/devices` | 设备 CRUD |
| 数据点 | `GET/POST/PUT/DELETE /api/devices/{id}/datapoints` | 数据点管理 |
| 通道管理 | `GET/POST/PUT/DELETE /api/channels` | 发送通道管理 |
| 规则管理 | `GET/POST/PUT/DELETE /api/rules` | 规则配置 |
| 虚拟节点 | `GET/POST/PUT/DELETE /api/virtual-nodes/points` | 虚拟数据点 |
| 实时数据 | `GET /api/devices/{id}/datapoints/realtime` | 实时数据快照 |
| 网关状态 | `GET /api/gateway/status` | 网关运行状态 |
| 枚举选项 | `GET /api/enums/*` | 获取枚举选项列表 |

### 请求示例

#### 创建设备

```http
POST /api/devices
Content-Type: application/json

{
  "name": "车间 A-PLC01",
  "code": "PLC001",
  "description": "一号 PLC",
  "protocol": 1,
  "address": "192.168.1.100",
  "port": 502,
  "isEnabled": true,
  "pollingIntervalMs": 1000
}
```

#### 创建数据点

```http
POST /api/devices/1/datapoints
Content-Type: application/json

{
  "name": "温度",
  "tag": "PLC001.Temperature",
  "description": "环境温度",
  "address": "40001",
  "dataType": 6,
  "unit": "℃",
  "modbusSlaveId": 1,
  "modbusFunctionCode": 3,
  "isEnabled": true
}
```

### WebSocket 订阅

```javascript
// 单 Tag 订阅
const ws = new WebSocket('ws://localhost:5000/ws?tag=PLC001.Temperature');

// 多 Tag 订阅
const ws = new WebSocket('ws://localhost:5000/ws?tags=PLC001.Temperature,PLC001.Pressure');

// 设备订阅
const ws = new WebSocket('ws://localhost:5000/ws?device=PLC001');

// 全量订阅
const ws = new WebSocket('ws://localhost:5000/ws');

ws.onmessage = (event) => {
  const data = JSON.parse(event.data);
  console.log('收到数据:', data);
};
```

---

## 📘 开发指南

### 扩展采集协议

#### 步骤 1：添加枚举值

编辑 `Domain/Enums/CollectionProtocol.cs`：

```csharp
public enum CollectionProtocol
{
    Simulator = 0,
    Modbus = 1,
    OpcUa = 2,        // 新增
    S7 = 3            // 新增
}
```

#### 步骤 2：实现策略接口

创建 `Infrastructure/Strategies/Collection/OpcUaCollectionStrategy.cs`：

```csharp
using EdgeGateway.Domain.Entities;
using EdgeGateway.Domain.Interfaces;

namespace EdgeGateway.Infrastructure.Strategies.Collection;

public class OpcUaCollectionStrategy : ICollectionStrategy
{
    public string ProtocolName => "OpcUa";
    public bool IsConnected => true;

    public Task ConnectAsync(Device device, CancellationToken cancellationToken = default)
    {
        // TODO: 实现 OPC UA 连接逻辑
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // TODO: 实现断开连接逻辑
        return Task.CompletedTask;
    }

    public async Task ReadAsync(
        IEnumerable<DataPoint> dataPoints,
        Action<CollectedData> callback,
        CancellationToken cancellationToken = default)
    {
        // TODO: 实现 OPC UA 读取逻辑
        await Task.CompletedTask;
    }
}
```

#### 步骤 3：注册策略

编辑 `Host/ServiceCollectionExtensions.cs`：

```csharp
services.AddTransient<OpcUaCollectionStrategy>();

// 在注册器中注册
registry.Register<OpcUaCollectionStrategy>(CollectionProtocol.OpcUa);
```

#### 步骤 4：添加 NuGet 包

```bash
cd EdgeGateway.Infrastructure
dotnet add package OPCFoundation.NetStandard.Opc.Ua
```

### 扩展发送协议

类似采集协议，参考 `DEVELOPER_GUIDE.md` 获取详细步骤。

### 添加 API 接口

#### 创建 Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using EdgeGateway.WebApi.DTOs.Response;

namespace EdgeGateway.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 测试接口
    /// </summary>
    [HttpGet("hello")]
    public ActionResult<ApiResponse<string>> Hello()
    {
        return Ok(ApiResponse.Ok("Hello, EdgeGateway!"));
    }
}
```

### 前端开发

#### 创建 API 调用

```typescript
// edge-gateway-ui/src/api/device.ts
import request from '@/utils/request'

export interface Device {
  id: number
  name: string
  code: string
  protocol: number
}

export function getDevices() {
  return request<Device[]>({
    url: '/api/devices',
    method: 'get'
  })
}
```

#### 创建页面组件

```vue
<!-- edge-gateway-ui/src/views/DevicesView.vue -->
<template>
  <div class="devices-view">
    <PageHeader title="设备管理" />
    
    <el-table :data="deviceList">
      <el-table-column prop="name" label="设备名称" />
      <el-table-column prop="code" label="设备编码" />
      <el-table-column prop="protocolName" label="协议类型" />
    </el-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getDevices } from '@/api/device'

const deviceList = ref([])

onMounted(async () => {
  deviceList.value = await getDevices()
})
</script>
```

---

## ❓ 常见问题

### Q1: 数据库文件在哪里？

**A**: 默认在项目根目录的 `gateway.db`，可在 `appsettings.json` 中修改。

### Q2: 如何修改采集周期？

**A**: 
- 全局默认：修改 `GatewayOptions:Collection:DefaultPollingIntervalMs`
- 单个设备：通过 API 修改设备的 `PollingIntervalMs` 属性

### Q3: 采集的数据存在哪里？

**A**: 
- 实时数据：内存快照缓存（`DataCollectionService._dataSnapshot`）
- 配置数据：SQLite 数据库（`gateway.db`）
- 历史数据：需自行扩展存储（如 InfluxDB、TimescaleDB）

### Q4: 如何添加新的采集协议？

**A**: 参考 [开发指南](#-开发指南) 章节，或查看 `DEVELOPER_GUIDE.md`。

### Q5: WebSocket 连接不上？

**A**: 
1. 检查后端是否启动
2. 检查防火墙设置
3. 确认路径正确：`ws://localhost:5000/ws`

### Q6: 前端页面空白？

**A**: 
1. 检查浏览器控制台是否有错误
2. 确认后端 API 可访问
3. 检查 CORS 配置

### Q7: 如何重置数据库？

**A**: 删除 `gateway.db` 文件，重启应用会自动创建。

---

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

### 贡献流程

1. Fork 本项目
2. 创建功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'feat: Add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 提交 Pull Request

### 提交信息规范

```
feat: 新功能
fix: 修复 bug
docs: 文档更新
style: 代码格式调整
refactor: 重构代码
test: 测试相关
chore: 构建/工具相关
```

### 开发环境设置

```bash
# 克隆项目
git clone https://github.com/your-org/EdgeGateway.git
cd EdgeGateway

# 还原依赖
dotnet restore
cd edge-gateway-ui && npm install

# 运行测试
dotnet test

# 代码检查
dotnet format
```

---

## 📄 许可证

本项目采用 [MIT 许可证](./LICENSE)

```
Copyright (c) 2026 EdgeGateway

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
```

---

## 📞 联系方式

- **项目地址**: https://github.com/your-org/EdgeGateway
- **问题反馈**: https://github.com/your-org/EdgeGateway/issues
- **讨论区**: https://github.com/your-org/EdgeGateway/discussions

---

## 🙏 致谢

感谢以下开源项目：

- [.NET](https://dotnet.microsoft.com/) - 主框架
- [Vue.js](https://vuejs.org/) - 前端框架
- [Element Plus](https://element-plus.org/) - UI 组件库
- [NModbus4](https://github.com/NModbus4/NModbus4) - Modbus 协议库
- [MQTTnet](https://github.com/chkr1011/MQTTnet) - MQTT 客户端库
- [NCalc](https://github.com/ncalc/ncalc) - 表达式计算引擎
- [Entity Framework Core](https://docs.microsoft.com/ef/core/) - ORM 框架

---

## 📚 相关文档

| 文档 | 描述 | 适合人群 |
|------|------|----------|
| [README.md](./README.md) | 项目说明和快速开始 | 所有人 |
| [docs/DEVELOPER_GUIDE.md](./docs/DEVELOPER_GUIDE.md) | 📘 详细的扩展开发指南 | 开发者 |
| [docs/AI_AGENT_GUIDE.md](./docs/AI_AGENT_GUIDE.md) | 🤖 AI Agent 开发指南 | AI 助手 |
| [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) | 架构设计文档 | 架构师、高级开发者 |
| [docs/API_REFERENCE.md](./docs/API_REFERENCE.md) | 完整的 API 接口参考 | 前端开发、API 使用者 |
| [docs/DATABASE_RELATIONSHIPS.md](./docs/DATABASE_RELATIONSHIPS.md) | 数据库关系说明 | 开发者 |
| [edge-gateway-ui/README.md](./edge-gateway-ui/README.md) | 前端项目说明 | 前端开发者 |

---

**🎉 感谢使用 EdgeGateway！**
