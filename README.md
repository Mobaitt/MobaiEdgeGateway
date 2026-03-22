# EdgeGateway 🚀

> 一个面向工业边缘场景的采集网关项目，负责设备接入、点位采集、控制写入、规则处理、虚拟点计算和多通道数据转发。

## ✨ 项目简介

`EdgeGateway` 是一个前后端一体的工业边缘网关系统，目标不是只做“读数据”，而是提供一整套完整链路：

- 🔌 设备接入与管理
- 📍 数据点配置与实时采集
- 🎛️ 点位控制写入
- 🧠 规则引擎处理
- 🧮 虚拟点计算
- 📤 多通道数据发送
- 🔄 自动重连与运行状态监控
- 🖥️ 可视化管理后台

当前项目由以下部分组成：

- `ASP.NET Core WebApi` 后端服务
- `Vue 3 + Element Plus` 前端管理界面
- `SQLite` 本地配置数据库
- 基于策略模式的采集协议与发送协议扩展体系

---

## 🧱 项目结构

### 后端项目

- `EdgeGateway.Domain`
  领域实体、枚举、接口、配置选项

- `EdgeGateway.Application`
  应用服务层，负责采集调度、点位控制、发送调度、设备管理、规则执行、运行状态维护

- `EdgeGateway.Infrastructure`
  数据库上下文、仓储实现、采集策略、发送策略、规则引擎、虚拟点基础设施

- `EdgeGateway.Host`
  依赖注入注册、数据库初始化、启动扩展

- `EdgeGateway.WebApi`
  HTTP API 入口

### 前端项目

- `edge-gateway-ui`
  管理后台，负责设备、点位、通道、规则、虚拟点的配置与运行状态展示

---

## 🔥 当前能力

### 📟 设备管理

- 新增、编辑、删除设备
- 启用/禁用设备
- 配置设备地址、端口、协议、采集周期
- 配置自动重连参数
- 实时展示设备运行状态、错误信息、失败率、重连进度

### 📍 数据点管理

- 新增、编辑、删除数据点
- 分页查询、筛选、实时值查看
- 配置数据类型、寄存器地址、功能码、从站号、字节序、寄存器长度
- 配置点位是否启用
- 配置点位是否允许控制

### 🎛️ 点位控制

- 通过 `tag + value` 进行控制写入
- 按点位配置进行类型归一化
- 写入完成后刷新实时快照
- 支持 `Modbus` 与 `Simulator`

### 📡 数据采集

- `Modbus` 采集
- `Simulator` 模拟采集
- 设备级轮询
- 实时快照缓存
- 聚合后统一发送
- 自动重连与失败统计

### 🧠 数据处理

- 规则引擎执行
- 限幅、校验、转换、计算
- 虚拟数据点表达式计算
- 快照过期处理

### 📤 数据发送

- 本地文件
- MQTT
- HTTP
- WebSocket

---

## ⚙️ 核心运行流程

### 1. 采集主流程 📥

1. 应用启动，初始化依赖注入和数据库
2. `DataCollectionService` 启动聚合器与虚拟点计算器
3. 加载所有启用设备
4. 按设备启动独立采集任务
5. 通过 `CollectionStrategyRegistry` 解析对应采集策略
6. 采集值进入实时快照
7. 规则引擎处理采集结果
8. 聚合器定时将快照批量交给 `DataSendService`
9. `DataSendService` 再按通道分发到发送策略

### 2. 点位控制流程 🎯

1. 前端调用 `/api/devices/datapoints/control`
2. 请求体只提交：

```json
{
  "tag": "Device01.Temperature",
  "value": 123
}
```

3. 后端通过 `tag` 定位数据点
4. 校验点位是否启用、是否允许控制
5. 根据设备协议解析采集策略
6. 执行 `WriteAsync`
7. 回读或覆盖快照
8. 前端列表立即刷新

### 3. 设备状态流程 📈

系统会持续维护每台设备的运行状态，包括：

- 当前状态
- 状态说明
- 最近错误
- 最近连接时间
- 最近读取时间
- 最近失败时间
- 当前重连轮次/次数
- 当前失败率

前端设备页会轮询这些状态并实时显示。

---

## 🔄 自动重连机制

每台设备支持以下重连参数：

- `ReconnectEnabled`
  是否启用自动重连

- `ReconnectRetryCount`
  单轮重连尝试次数

- `ReconnectRetryDelayMs`
  单轮内每次尝试之间的间隔

- `ReconnectIntervalMs`
  一轮全部失败后，下一轮开始前的等待时间

- `MaxConsecutiveReadFailures`
  连续读取失败阈值

- `ReadFailureWindowSize`
  失败率统计窗口大小

- `ReadFailureRateThresholdPercent`
  失败率阈值

### 触发重连的典型场景 🛠️

- 连接设备失败
- 已连接，但读取过程中出现连接级异常
- 连续读取失败达到阈值
- 在统计窗口内失败率超过阈值

### 三个最容易混淆的参数说明 💡

- `MaxConsecutiveReadFailures`
  看“连续失败多少次就重连”

- `ReadFailureWindowSize`
  看“最近多少次读取结果参与统计”

- `ReadFailureRateThresholdPercent`
  看“窗口内失败率达到多少就重连”

例如：

- 窗口 `10`
- 阈值 `50`

表示最近 `10` 次读取里，如果失败达到 `5` 次或以上，就触发重连。

---

## 🗄️ 数据库机制

数据库使用 `SQLite`。

启动时会自动执行：

- `EnsureCreatedAsync()`：数据库不存在则创建
- 历史兼容迁移：处理旧字段迁移到新结构
- 按实体自动补齐缺失字段

### 当前自动同步能力 ✅

- 自动补新增字段
- 自动保持已有历史数据

### 当前不会自动处理的内容 ⚠️

- 删字段
- 字段改名
- 字段类型变更
- 索引/约束结构变更

相关入口：

- `EdgeGateway.Host/ServiceCollectionExtensions.cs`

---

## 🧩 关键代码入口

### 后端核心入口

- `EdgeGateway.Host/ServiceCollectionExtensions.cs`
  服务注册、数据库初始化、启动扩展

- `EdgeGateway.Application/Services/DataCollectionService.cs`
  采集调度中心

- `EdgeGateway.Application/Services/DataPointControlService.cs`
  点位控制写入

- `EdgeGateway.Application/Services/DataSendService.cs`
  数据聚合发送

- `EdgeGateway.Application/Services/CollectionStrategyRegistry.cs`
  采集策略注册与解析

- `EdgeGateway.WebApi/Controllers/DevicesController.cs`
  设备、点位、控制相关 API

- `EdgeGateway.Infrastructure/Data/GatewayDbContext.cs`
  数据库上下文

### 前端核心入口

- `edge-gateway-ui/src/layouts/MainLayout.vue`
  主布局、导航、主题切换

- `edge-gateway-ui/src/views/DevicesView.vue`
  设备管理页

- `edge-gateway-ui/src/views/DataPointsView.vue`
  数据点管理与控制页

- `edge-gateway-ui/src/views/ChannelsView.vue`
  通道管理页

- `edge-gateway-ui/src/views/MappingsView.vue`
  通道与数据点映射页

- `edge-gateway-ui/src/views/RulesView.vue`
  规则管理页

---

## 🧪 当前默认已接入协议

### 采集协议 📥

- `Simulator`
- `Modbus`

### 发送协议 📤

- `LocalFile`
- `Mqtt`
- `Http`
- `WebSocket`

---

## 🧰 如何扩展采集策略

如果你要新增一种采集协议，比如 `OPC UA`、`S7`、`BACnet`，建议按下面步骤来。

### 第一步：增加协议枚举 🏷️

在 `EdgeGateway.Domain/Enums` 中补充 `CollectionProtocol` 枚举值。

### 第二步：实现采集策略 🛠️

实现 `ICollectionStrategy`：

- `ConnectAsync(Device device)`
- `DisconnectAsync()`
- `ReadAsync(IEnumerable<DataPoint> dataPoints, Action<CollectedData> callback)`
- `WriteAsync(DataPoint dataPoint, object? value)`
- `IsConnected`
- `ProtocolName`

可以参考现有实现：

- `ModbusCollectionStrategy`
- `SimulatorCollectionStrategy`

### 第三步：注册到 DI 🧩

在 `EdgeGateway.Host/ServiceCollectionExtensions.cs` 中注册：

```csharp
services.AddTransient<YourCollectionStrategy>();
```

### 第四步：注册到采集策略注册器 🧠

同样在 `ServiceCollectionExtensions.cs` 中：

```csharp
registry.Register<YourCollectionStrategy>(CollectionProtocol.YourProtocol);
```

### 第五步：补充前端配置项 🖥️

如果新协议需要前端配置：

- 枚举接口补充协议选项
- 设备编辑弹窗增加协议说明与参数项
- 前端类型定义同步更新

### 设计建议 ✅

- `ReadAsync` 尽量批量读取，减少通信往返
- 连接级异常要抛给上层，不要吞掉
- `WriteAsync` 要严格按点位配置的数据类型执行
- 协议策略只处理协议细节，不要把调度逻辑写进策略内部

---

## 📤 如何扩展发送策略

### 第一步：补充发送协议枚举 🏷️

在 `EdgeGateway.Domain/Enums` 中增加新的 `SendProtocol`。

### 第二步：实现发送策略 🛠️

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

### 第三步：注册 DI 🧩

```csharp
services.AddTransient<YourSendStrategy>();
```

### 第四步：注册到发送策略注册器 📦

```csharp
registry.Register<YourSendStrategy>(SendProtocol.YourProtocol);
```

### 第五步：同步前端通道配置 🖥️

如果新协议有额外参数：

- 更新实体与 DTO
- 更新前端类型定义
- 更新通道编辑弹窗

---

## 🧠 如何扩展“采集模式”或处理逻辑

这个项目的“采集模式”并不是一个单独开关，而是由以下几个层次组合出来的：

- 设备级轮询
- 协议策略采集
- 规则引擎处理
- 虚拟点计算
- 聚合快照分发

### 扩展设备级采集调度 ⏱️

主要修改：

- `DataCollectionService`

适合处理：

- 新轮询策略
- 失败判定规则
- 重连规则
- 状态统计逻辑

### 扩展点位数据处理 🧪

适合处理：

- 数据质量判定
- 值标准化
- 自定义过滤
- 写入后回读策略

### 扩展规则模式 📐

适合处理：

- 限幅
- 值转换
- 合法性校验
- 默认值替换

### 扩展虚拟点模式 🧮

适合处理：

- 表达式计算
- 多点聚合
- 派生指标

---

## 🖥️ 前端配置入口

当前前端主要配置项包括：

### 设备管理页 📟

- 协议
- 地址/端口
- 采集周期
- 自动重连参数
- 失败阈值与失败率参数

### 数据点管理页 📍

- 地址
- 数据类型
- 功能码
- 从站号
- 字节序
- 寄存器长度
- 是否启用
- 是否允许控制

### 通道管理页 📤

- 发送协议
- 目标地址
- 协议专属参数

### 规则管理页 🧠

- 规则类型
- 优先级
- 失败处理方式

### 虚拟点管理页 🧮

- 表达式
- 数据类型
- 依赖点位

---

## 🎨 前端主题体系

前端已经支持两套主题：

- 🌙 暗色模式
  当前默认主题，偏工业监控风格

- ☀️ 明亮模式
  更适合白天办公场景

主题相关文件：

- `edge-gateway-ui/src/styles/theme.css`
- `edge-gateway-ui/src/styles/global.css`
- `edge-gateway-ui/src/styles/page-patterns.css`
- `edge-gateway-ui/src/composables/useTheme.ts`

---

## 🖥️ 运行环境

### 后端

- `.NET 8 SDK`

### 前端

- `Node.js 18+`
- `npm`

---

## ▶️ 启动方式

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

---

## 🧪 测试与验证

如有测试项目，可执行：

```bash
dotnet test EdgeGateway.Tests
```

推荐开发时最少验证这几项：

- ✅ 设备新增/编辑/启停
- ✅ 数据点采集与实时显示
- ✅ 点位控制写入
- ✅ 通道发送
- ✅ 重连逻辑
- ✅ 前端构建

---

## 📝 开发建议

- 先补枚举，再补策略实现，再补注册器，再补前端配置
- 采集策略只关注协议细节，不处理全局调度
- 重连、失败统计、轮询节奏统一放在采集调度层
- 控制写入必须保证“写入类型”和“读取解析”一致
- 修改实体字段后，要同步检查 DTO、前端类型和弹窗表单
- 涉及历史数据库兼容时，优先考虑启动时自动补列机制

---

## 📌 当前文档约定

仓库中仅保留本文件作为 Markdown 文档入口。📘

如果后续要继续补文档，建议优先扩展：

- API 调用示例
- 新增协议模板
- 前端页面结构说明
- 数据库实体关系说明

