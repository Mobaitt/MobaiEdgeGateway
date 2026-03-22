# EdgeGateway

EdgeGateway 是一个工业边缘网关项目，用于统一管理设备接入、数据采集、点位控制、规则处理、虚拟点计算和多通道数据转发。

项目当前同时包含：

- `ASP.NET Core WebApi` 后端
- `Vue 3 + Element Plus` 前端管理界面
- `SQLite` 本地配置数据库
- 基于策略模式的采集协议和发送协议扩展机制

## 1. 当前能力

### 设备与点位

- 设备新增、编辑、启停、删除
- 数据点新增、编辑、删除、分页查询
- 点位实时值读取
- 点位控制写入
- 可配置“点位是否允许控制”

### 采集

- Modbus 采集
- Simulator 模拟采集
- 设备级轮询
- 实时快照缓存
- 数据聚合后统一分发
- 自动重连

### 自动重连

设备支持以下重连控制项：

- `ReconnectEnabled`：是否启用自动重连
- `ReconnectRetryCount`：单轮重连尝试次数
- `ReconnectRetryDelayMs`：单轮内每次重试间隔
- `ReconnectIntervalMs`：一整轮失败后，下一轮开始前的等待时间
- `MaxConsecutiveReadFailures`：连续读取失败阈值
- `ReadFailureWindowSize`：读取失败率统计窗口
- `ReadFailureRateThresholdPercent`：读取失败率阈值

触发重连的典型场景：

- 设备连接失败
- 设备已连接，但读取时出现连接级异常
- 连续读取失败达到阈值
- 在统计窗口内失败占比超过阈值

### 数据处理

- 规则引擎处理采集结果
- 虚拟数据点表达式计算
- 采集快照过期判断

### 数据发送

- 本地文件
- MQTT
- HTTP
- WebSocket

## 2. 项目结构

### 后端项目

- `EdgeGateway.Domain`
  领域模型、枚举、接口、配置选项

- `EdgeGateway.Application`
  应用服务层，包含采集调度、发送调度、设备管理、控制、规则和虚拟点逻辑

- `EdgeGateway.Infrastructure`
  仓储、数据库上下文、采集策略实现、发送策略实现、规则与虚拟点基础设施

- `EdgeGateway.Host`
  服务注册、数据库初始化、启动扩展

- `EdgeGateway.WebApi`
  HTTP API 入口

### 前端项目

- `edge-gateway-ui`
  管理端界面，负责设备、点位、通道、规则、虚拟点配置与运行状态展示

## 3. 核心运行流程

### 采集主流程

1. 应用启动后执行依赖注入和数据库初始化
2. `DataCollectionService` 启动聚合器和虚拟点计算器
3. 加载所有启用设备
4. 按设备启动独立采集任务
5. 通过 `CollectionStrategyRegistry` 按协议解析采集策略
6. 采集结果进入实时快照
7. 规则引擎处理后写入快照
8. 聚合器周期性将快照批量交给 `DataSendService`
9. `DataSendService` 再按通道协议分发给发送策略

### 点位控制流程

1. 前端提交 `tag + value`
2. 后端通过标签定位数据点
3. 校验该点位是否允许控制
4. 调用对应采集策略的 `WriteAsync`
5. 回读或覆盖实时快照
6. 前端实时列表刷新显示最新值

### 设备状态流程

`DataCollectionService` 内部维护每台设备的运行状态，包括：

- 当前状态
- 状态说明
- 最近异常
- 最近连接时间
- 最近读取时间
- 最近失败时间
- 当前重连轮次/次数
- 当前失败率

前端设备页会轮询这些状态并显示。

## 4. 数据库机制

数据库使用 `SQLite`。

启动时会执行：

- `EnsureCreatedAsync()`：不存在则创建数据库
- 历史特殊迁移：例如旧字段迁移到新结构
- 按实体自动补齐缺失字段

当前实现特性：

- 会根据实体定义自动补新增列
- 不自动处理删列
- 不自动处理字段改名
- 不自动处理字段类型变更
- 不自动处理索引/约束变更

相关入口：

- `EdgeGateway.Host/ServiceCollectionExtensions.cs`

## 5. 运行环境

### 后端

- `.NET 8 SDK`

### 前端

- `Node.js 18+`
- `npm`

## 6. 启动方式

### 后端启动

```bash
dotnet build EdgeGateway.sln
dotnet run --project EdgeGateway.WebApi
```

### 前端启动

```bash
cd edge-gateway-ui
npm install
npm run dev
```

### 前端打包

```bash
cd edge-gateway-ui
npm run build
```

## 7. 关键代码入口

### 服务注册与启动初始化

- `EdgeGateway.Host/ServiceCollectionExtensions.cs`

### 采集调度

- `EdgeGateway.Application/Services/DataCollectionService.cs`

### 点位控制

- `EdgeGateway.Application/Services/DataPointControlService.cs`

### 设备与点位 API

- `EdgeGateway.WebApi/Controllers/DevicesController.cs`

### 数据库上下文

- `EdgeGateway.Infrastructure/Data/GatewayDbContext.cs`

### 采集策略

- `EdgeGateway.Domain/Interfaces/ICollectionStrategy.cs`
- `EdgeGateway.Application/Services/CollectionStrategyRegistry.cs`
- `EdgeGateway.Infrastructure/Strategies/Collection/ModbusCollectionStrategy.cs`
- `EdgeGateway.Infrastructure/Strategies/Collection/SimulatorCollectionStrategy.cs`

### 发送策略

- `EdgeGateway.Domain/Interfaces/ISendStrategy.cs`
- `EdgeGateway.Domain/Interfaces/ISendStrategyRegistry.cs`
- `EdgeGateway.Application/Services/SendStrategyRegistry.cs`
- `EdgeGateway.Infrastructure/Strategies/Send/*.cs`

## 8. 如何扩展采集协议

新增一个采集协议时，建议按下面步骤进行。

### 第一步：补充协议枚举

在 `EdgeGateway.Domain/Enums` 中补充新的 `CollectionProtocol` 枚举值。

### 第二步：实现采集策略

实现 `ICollectionStrategy`：

- `ConnectAsync(Device device)`
- `DisconnectAsync()`
- `ReadAsync(IEnumerable<DataPoint> dataPoints, Action<CollectedData> callback)`
- `WriteAsync(DataPoint dataPoint, object? value)`
- `IsConnected`
- `ProtocolName`

建议参考：

- `ModbusCollectionStrategy`
- `SimulatorCollectionStrategy`

### 第三步：在 DI 中注册策略实现

在 `EdgeGateway.Host/ServiceCollectionExtensions.cs` 中注册：

```csharp
services.AddTransient<YourCollectionStrategy>();
```

### 第四步：注册到采集策略注册器

同样在 `ServiceCollectionExtensions.cs` 中：

```csharp
registry.Register<YourCollectionStrategy>(CollectionProtocol.YourProtocol);
```

### 第五步：前端补充协议选项

如果该协议需要在前端配置：

- 枚举接口返回中补充协议
- 设备编辑弹窗增加对应说明和参数输入项

### 设计建议

- `ReadAsync` 内部尽量批量读取，减少通信开销
- 遇到连接级异常时应抛出，让上层采集调度决定是否重连
- `WriteAsync` 要保证写入数据类型与点位配置一致
- 不要把设备级状态持久化在单例共享对象里，策略本身应保持设备实例隔离

## 9. 如何扩展发送策略

### 第一步：补充发送协议枚举

在 `EdgeGateway.Domain/Enums` 中增加新的 `SendProtocol`。

### 第二步：实现发送策略

实现 `ISendStrategy`：

- `InitializeAsync(Channel channel)`
- `SendAsync(SendPackage package)`
- `DisposeAsync()`
- `ProtocolName`

参考现有实现：

- `LocalFileSendStrategy`
- `MqttSendStrategy`
- `HttpSendStrategy`
- `WebSocketSendStrategy`

### 第三步：注册 DI

在 `ServiceCollectionExtensions.cs` 中注册：

```csharp
services.AddTransient<YourSendStrategy>();
```

### 第四步：注册到发送策略注册器

```csharp
registry.Register<YourSendStrategy>(SendProtocol.YourProtocol);
```

### 第五步：前端补充通道配置项

如果新协议需要额外参数：

- 更新通道实体/DTO
- 更新前端类型定义
- 更新通道编辑弹窗

## 10. 如何扩展采集模式或处理逻辑

这个项目的“采集模式”本质上由以下几层组成：

- 设备级轮询
- 协议策略采集
- 规则引擎处理
- 虚拟点计算
- 聚合快照分发

如果你要扩展采集行为，通常会落在下面几类位置：

### 扩展设备级采集调度

修改：

- `DataCollectionService`

适合处理：

- 新的轮询调度策略
- 新的失败判定规则
- 新的重连规则
- 新的状态统计逻辑

### 扩展点位数据处理

适合处理：

- 采集值质量判定
- 采集结果标准化
- 自定义过滤逻辑
- 控制写入后的回读策略

### 扩展规则模式

适合处理：

- 采集限幅
- 值转换
- 合法性校验
- 默认值替换

### 扩展虚拟点模式

适合处理：

- 表达式计算
- 多点聚合
- 派生指标

## 11. 前端配置入口

当前前端主要配置入口包括：

- 设备管理
  - 协议
  - 地址/端口
  - 采集周期
  - 自动重连参数

- 数据点管理
  - 地址
  - 数据类型
  - Modbus 功能码/从站号/字节序/寄存器长度
  - 是否启用
  - 是否允许控制

- 通道管理
  - 发送协议
  - 目标地址
  - 协议相关参数

- 规则管理
  - 规则类型
  - 优先级
  - 失败处理方式

- 虚拟点管理
  - 表达式
  - 数据类型
  - 依赖点位

## 12. 当前默认已接入协议

### 采集协议

- `Simulator`
- `Modbus`

### 发送协议

- `LocalFile`
- `Mqtt`
- `Http`
- `WebSocket`

## 13. 开发建议

- 新增协议时，先补领域枚举，再补策略实现，再补注册器，再补前端
- 采集策略内部只处理协议细节，不要把调度逻辑塞进策略里
- 重连、状态统计、轮询节奏统一放在 `DataCollectionService`
- 写入控制要优先保证“写入类型”和“读取解析”一致
- 历史数据库兼容尽量通过启动时自动补列处理
- 修改实体字段后，要确认前端 DTO 和弹窗配置同步更新

## 14. 当前文档约定

仓库中仅保留本文件作为项目文档入口。
