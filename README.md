# 边缘采集网关 - EdgeGateway

基于 C# .NET 8 实现的边缘采集网关，采用**策略模式 + 分层架构**设计。

---

## 项目结构

```
EdgeGateway/
├── EdgeGateway.Domain/                    # 领域层（核心抽象，无任何外部依赖）
│   ├── Entities/
│   │   ├── Device.cs                      # 设备实体
│   │   ├── DataPoint.cs                   # 数据点实体
│   │   ├── Channel.cs                     # 发送通道实体
│   │   └── ChannelDataPointMapping.cs     # 通道-数据点多对多映射
│   ├── Enums/
│   │   └── Enums.cs                       # 协议枚举、数据类型枚举
│   └── Interfaces/
│       ├── ICollectionStrategy.cs         # 采集策略接口 ★
│       ├── ISendStrategy.cs               # 发送策略接口 ★
│       └── IRepositories.cs               # 仓储接口
│
├── EdgeGateway.Infrastructure/            # 基础设施层（EF Core + 策略实现）
│   ├── Data/
│   │   └── GatewayDbContext.cs            # EF Core DbContext（SQLite）
│   ├── Repositories/
│   │   └── Repositories.cs               # 仓储实现
│   └── Strategies/
│       ├── Collection/
│       │   ├── SimulatorCollectionStrategy.cs  # 模拟数据采集（无需硬件）
│       │   └── ModbusCollectionStrategy.cs     # Modbus TCP/RTU 采集框架
│       └── Send/
│           ├── MqttSendStrategy.cs             # MQTT 发送
│           ├── HttpSendStrategy.cs             # HTTP REST 发送
│           └── LocalFileSendStrategy.cs        # 本地文件写入
│
├── EdgeGateway.Application/              # 应用层（业务编排，不含框架细节）
│   └── Services/
│       ├── StrategyRegistry.cs           # 策略注册器（工厂）
│       ├── DataCollectionService.cs      # 采集调度服务
│       ├── DataSendService.cs            # 发送分发服务
│       └── DeviceManagementService.cs    # 设备/通道 CRUD 管理
│
└── EdgeGateway.Host/                     # 宿主层（DI注册、程序入口）
    ├── Program.cs                        # 程序入口 + HostedService
    └── ServiceCollectionExtensions.cs    # DI扩展方法
```

---

## 架构设计

### 采集策略模式（Collection Strategy）

```
设备(Device) --协议类型--> CollectionStrategyRegistry（注册器/工厂）
                                  |
              ┌───────────────────┼─────────────────────┐
              ▼                   ▼                     ▼
  SimulatorStrategy         ModbusStrategy         OpcUaStrategy
  （模拟随机数据）          （读Modbus寄存器）     （读OPC节点）
```

### 发送策略模式（Send Strategy）

```
采集数据 --> DataSendService --> 按通道映射分发
                                      |
              ┌───────────────────────┼───────────────────────┐
              ▼                       ▼                       ▼
        MqttSendStrategy       HttpSendStrategy       LocalFileSendStrategy
        （发布到Broker）       （POST到REST接口）      （写入本地文件）
```

### 数据点与通道的多对多映射

```
Device → DataPoint（1:N）
Channel ←→ DataPoint（M:N，通过 ChannelDataPointMapping 关联表）

含义：选择哪些数据点 → 绑定到哪个发送通道 → 数据就通过那条通道发出去
```

---

## 快速开始

```bash
cd EdgeGateway.Host
dotnet run
```

程序启动后会：
1. 自动创建 SQLite 数据库 `gateway.db`
2. 写入种子数据（1个模拟设备 + 2个数据点 + 1个本地文件通道）
3. 开始每2秒采集一次数据，输出到 `./output/data.json`

---

## 扩展新采集协议

1. 在 `Infrastructure/Strategies/Collection/` 新建实现类，继承 `ICollectionStrategy`
2. 在 `Enums.cs` 的 `CollectionProtocol` 枚举中添加新类型
3. 在 `ServiceCollectionExtensions.cs` 中注册：
   ```csharp
   services.AddTransient<OpcUaCollectionStrategy>();
   registry.Register<OpcUaCollectionStrategy>(CollectionProtocol.OpcUa);
   ```

## 扩展新发送方式

1. 在 `Infrastructure/Strategies/Send/` 新建实现类，继承 `ISendStrategy`
2. 在 `Enums.cs` 的 `SendProtocol` 枚举中添加新类型
3. 在 `ServiceCollectionExtensions.cs` 中注册：
   ```csharp
   services.AddTransient<KafkaSendStrategy>();
   registry.Register<KafkaSendStrategy>(SendProtocol.Kafka);
   ```

---

## 推荐引入的 NuGet 包

| 功能 | NuGet 包 |
|------|---------|
| Modbus TCP/RTU | `FluentModbus` 或 `NModbus4` |
| OPC UA | `OPCFoundation.NetStandard.Opc.Ua` |
| MQTT | `MQTTnet` |
| Kafka | `Confluent.Kafka` |
| S7 PLC | `S7.Net` |
