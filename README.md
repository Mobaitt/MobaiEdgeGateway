# 边缘采集网关 - EdgeGateway

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Vue](https://img.shields.io/badge/Vue-3.4-4FC08D?logo=vue.js)](https://vuejs.org/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

> **边缘计算数据采集网关** - 支持多种工业协议，策略模式架构，易于扩展

---

## 📖 项目简介

EdgeGateway 是一款基于 **.NET 8** 和 **Vue 3** 开发的边缘计算数据采集网关。采用**策略模式 + 分层架构**设计，支持从工业设备（PLC、传感器等）采集数据，并通过多种方式（MQTT、HTTP、WebSocket 等）发送到云端或本地系统。

### ✨ 核心特性

- 🔄 **多协议采集** - 支持 Modbus TCP/RTU、OPC UA、西门子 S7 等工业协议
- 📤 **多方式发送** - 支持 MQTT、HTTP REST、WebSocket、Kafka、本地文件等
- 🧮 **虚拟节点** - 支持表达式计算，创建派生数据点
- ⚙️ **规则引擎** - 支持数据限制、转换、校验、计算规则
- 🎨 **现代 UI** - Vue 3 + TypeScript + Element Plus，工业监控风格
- 📦 **模块化设计** - 策略模式架构，易于扩展新协议
- 💾 **轻量存储** - SQLite 数据库，便于部署

---

## 🏗️ 项目结构

```
EdgeGateway/
├── EdgeGateway.Domain/              # 领域层（核心抽象，无外部依赖）
│   ├── Entities/                    # 实体类
│   ├── Enums/                       # 枚举定义
│   └── Interfaces/                  # 核心接口
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
│
├── EdgeGateway.Host/                # 控制台宿主
├── EdgeGateway.WebApi/              # Web API 宿主
└── edge-gateway-ui/                 # Vue 3 前端项目
```

### 📊 依赖关系

```
┌─────────────────┐
│     WebApi      │
│      Host       │
└────────┬────────┘
         │
┌────────▼────────┐
│   Application   │
└────────┬────────┘
         │
┌────────▼────────┐
│  Infrastructure │
└────────┬────────┘
         │
┌────────▼────────┐
│     Domain      │
└─────────────────┘
```

---

## 🚀 快速开始

### 环境要求

- .NET 8.0 SDK
- Node.js 18+
- SQLite（内置，无需安装）

### 后端启动

```bash
# 克隆项目
cd EdgeGateway

# 还原依赖
dotnet restore

# 运行控制台宿主
cd EdgeGateway.Host
dotnet run

# 或运行 Web API
cd EdgeGateway.WebApi
dotnet run
```

启动后会自动：
1. 创建 SQLite 数据库 `gateway.db`
2. 写入种子数据（模拟设备 + 数据点 + 通道）
3. 开始采集数据（默认 2 秒/周期）

### 前端启动

```bash
cd edge-gateway-ui

# 安装依赖
npm install

# 开发模式
npm run dev

# 构建生产版本
npm run build
```

访问 `http://localhost:5173` 查看管理界面。

---

## 📋 核心概念

### 设备（Device）

代表一个现场采集设备（如 PLC、传感器），包含：
- **协议类型**：Modbus、OPC UA、Simulator 等
- **连接信息**：IP 地址、端口、串口号
- **采集周期**：数据读取间隔（毫秒）

### 数据点（DataPoint）

设备上的一个监测点，包含：
- **Tag**：全局唯一标识
- **地址**：Modbus 寄存器地址或 OPC 节点
- **数据类型**：Int16、Float、Bool 等
- **工程量单位**：℃、MPa、m³/h 等

### 发送通道（Channel）

数据发送的目标，包含：
- **协议类型**：MQTT、HTTP、WebSocket 等
- **连接配置**：Broker 地址、Topic、认证信息
- **数据映射**：绑定哪些数据点发送

### 虚拟节点（VirtualNode）

基于表达式计算的派生数据点：
- **表达式**：`A + B * 2`、`Average(Temp1, Temp2, Temp3)`
- **依赖**：自动解析依赖的其他数据点
- **计算类型**：Sum、Average、Max、Min 等

### 规则（Rule）

数据处理规则：
- **限制规则**：数值范围、采集频率
- **转换规则**：线性变换、公式计算、查表转换
- **校验规则**：阈值、变化率、死区校验
- **计算规则**：自定义公式计算

---

## 🎯 架构设计

### 采集策略模式

```
┌─────────────┐
│   Device    │─── 协议类型 ───► CollectionStrategyRegistry
└─────────────┘                       │
                                      ├──► SimulatorStrategy
                                      ├──► ModbusStrategy
                                      └──► OpcUaStrategy
```

### 发送策略模式

```
采集数据 ──► DataSendService ──► 按通道映射分发
                                      │
                                      ├──► MqttSendStrategy
                                      ├──► HttpSendStrategy
                                      └──► LocalFileSendStrategy
```

### 数据映射关系

```
Device ──(1:N)──► DataPoint
Channel ──(M:N)──► DataPoint  （通过 ChannelDataPointMapping）

含义：选择哪些数据点 → 绑定到哪个通道 → 数据就通过该通道发送
```

---

## 📖 文档导航

### 快速开始
| 文档 | 描述 |
|------|------|
| [README.md](./README.md) | 项目说明和快速开始 |
| [REFACTORING_PLAN_V2.md](./REFACTORING_PLAN_V2.md) | 分包方案说明 |

### 开发文档
| 文档 | 描述 |
|------|------|
| [docs/DEVELOPER_GUIDE.md](./docs/DEVELOPER_GUIDE.md) | 📘 开发者指南 - 如何扩展功能 |
| [docs/AI_AGENT_GUIDE.md](./docs/AI_AGENT_GUIDE.md) | 🤖 AI Agent 开发指南 - 让 AI 帮你写代码 |
| [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) | 架构设计文档 |
| [docs/API_REFERENCE.md](./docs/API_REFERENCE.md) | API 接口参考 |

### 功能指南
| 文档 | 描述 |
|------|------|
| [RULES_AND_VIRTUAL_NODES_GUIDE.md](./RULES_AND_VIRTUAL_NODES_GUIDE.md) | 规则和虚拟节点指南 |
| [WEBSOCKET_APFOX_GUIDE.md](./WEBSOCKET_APFOX_GUIDE.md) | WebSocket 使用指南 |

---

## 🛠️ 技术栈

### 后端
- **框架**: .NET 8.0
- **ORM**: Entity Framework Core 8
- **数据库**: SQLite
- **Modbus**: NModbus4
- **MQTT**: MQTTnet
- **表达式计算**: NCalcSync
- **JSON**: Newtonsoft.Json

### 前端
- **框架**: Vue 3.4 + TypeScript
- **构建工具**: Vite 5
- **UI 组件库**: Element Plus 2.6
- **状态管理**: Pinia 2
- **路由**: Vue Router 4
- **HTTP**: Axios 1.6

---

## 📦 已支持的协议

### 采集协议
| 协议 | 状态 | 说明 |
|------|------|------|
| Simulator | ✅ 已支持 | 模拟器（生成随机数据） |
| Modbus TCP/RTU | ✅ 已支持 | 使用 NModbus4 库 |
| OPC UA | 🔲 待扩展 | 需引入 OPCFoundation 库 |
| 西门子 S7 | 🔲 待扩展 | 需引入 S7.Net 库 |

### 发送协议
| 协议 | 状态 | 说明 |
|------|------|------|
| MQTT | ✅ 已支持 | 发布到 MQTT Broker |
| HTTP REST | ✅ 已支持 | POST 到 HTTP 接口 |
| WebSocket | ✅ 已支持 | 推送给订阅客户端 |
| 本地文件 | ✅ 已支持 | 写入 JSON/NDJSON 文件 |
| Kafka | 🔲 待扩展 | 需引入 Confluent.Kafka 库 |

---

## 🔧 配置说明

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=gateway.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 数据库迁移

```bash
# 添加迁移
dotnet ef migrations add InitialCreate --project EdgeGateway.Infrastructure

# 更新数据库
dotnet ef database update --project EdgeGateway.Host
```

---

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本项目
2. 创建功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 提交 Pull Request

---

## 📄 许可证

本项目采用 [MIT 许可证](./LICENSE)

---

## 📞 联系方式

- 项目地址：https://github.com/your-org/EdgeGateway
- 问题反馈：https://github.com/your-org/EdgeGateway/issues

---

## 🙏 致谢

感谢以下开源项目：
- [.NET](https://dotnet.microsoft.com/)
- [Vue.js](https://vuejs.org/)
- [Element Plus](https://element-plus.org/)
- [NModbus4](https://github.com/NModbus4/NModbus4)
- [MQTTnet](https://github.com/chkr1011/MQTTnet)
