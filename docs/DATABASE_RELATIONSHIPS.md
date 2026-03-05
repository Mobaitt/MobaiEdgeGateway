# 数据库关系说明文档

## 概述

本文档详细说明 EdgeGateway 项目中设备与数据点的数据库关系，以及相关的实体关系模型。

## 数据库表结构

### 1. 核心实体表

#### Devices（设备表）
存储现场采集设备的基本信息。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| Name | string | 设备名称 |
| Code | string | 设备编码（唯一） |
| Description | string? | 设备描述 |
| Protocol | CollectionProtocol | 通信协议类型 |
| Address | string | IP 地址或串口号 |
| Port | int? | 通信端口 |
| IsEnabled | bool | 是否启用 |
| PollingIntervalMs | int | 采集周期（毫秒） |
| CreatedAt | DateTime | 创建时间 |
| UpdatedAt | DateTime | 最后更新时间 |

#### DataPoints（数据点表）
存储设备上的采集点信息（如寄存器地址、OPC 节点等）。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| DeviceId | int | 所属设备 ID（外键） |
| Name | string | 数据点名称 |
| **Tag** | string | **数据点标签（全局唯一）** |
| Description | string? | 描述 |
| Address | string | 数据地址 |
| DataType | DataValueType | 数据类型 |
| Unit | string? | 工程量单位 |
| ModbusSlaveId | byte? | Modbus 从站地址 |
| ModbusFunctionCode | int? | Modbus 功能码 |
| ModbusByteOrder | ModbusByteOrder? | 字节顺序 |
| RegisterLength | byte | 寄存器长度 |
| IsEnabled | bool | 是否启用 |
| CreatedAt | DateTime | 创建时间 |

#### VirtualDataPoints（虚拟数据点表）
存储通过表达式计算得出的虚拟数据点。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| DeviceId | int | 所属设备 ID（外键） |
| Name | string | 虚拟数据点名称 |
| **Tag** | string | **虚拟数据点标签（全局唯一）** |
| Description | string? | 描述 |
| Expression | string | 计算表达式 |
| CalculationType | CalculationType | 计算类型 |
| DataType | DataValueType | 数据类型 |
| Unit | string? | 单位 |
| IsEnabled | bool | 是否启用 |
| DependencyTags | string | 依赖的 Tags（JSON 数组） |
| LastCalculationTime | DateTime? | 上次计算时间 |
| LastValueJson | string? | 上次计算结果（JSON） |
| CreatedAt | DateTime | 创建时间 |

#### Channels（发送通道表）
存储数据上传通道的配置信息。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| Name | string | 通道名称 |
| Code | string | 通道编码（唯一） |
| Description | string? | 描述 |
| Protocol | SendProtocol | 发送协议类型 |
| Endpoint | string | 连接字符串或 Endpoint |
| MqttTopic | string? | MQTT 主题 |
| HttpMethod | string? | HTTP 方法 |
| WsSubscribeTopic | string? | WebSocket 订阅主题 |
| IsEnabled | bool | 是否启用 |
| CreatedAt | DateTime | 创建时间 |

### 2. 关联表

#### ChannelDataPointMappings（通道 - 数据点映射表）
存储通道与数据点之间的多对多映射关系。

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| ChannelId | int | 发送通道 ID（外键） |
| DataPointId | int? | 普通数据点 ID（外键） |
| VirtualDataPointId | int? | 虚拟数据点 ID（外键） |
| **DataPointTag** | string | **数据点 Tag 快照** |
| DataPointName | string | 数据点名称快照 |
| IsEnabled | bool | 是否启用 |
| CreatedAt | DateTime | 创建时间 |

**约束**：
- `DataPointId` 和 `VirtualDataPointId` 至少有一个有值
- 同一通道不能重复添加同一数据点

---

## 实体关系图

```
┌─────────────┐
│   Device    │
│   (设备)     │
└──────┬──────┘
       │ 1
       │
       │ N
       │
       ├──────────────────┐
       │                  │
       ▼ N                ▼ N
┌─────────────┐    ┌──────────────┐
│  DataPoint  │    │VirtualDataPoint│
│  (数据点)    │    │ (虚拟数据点)  │
└──────┬──────┘    └──────┬───────┘
       │ N                │ N
       │                  │
       │                  │
       └────────┬─────────┘
                │ N
                │
                │
          ┌─────▼──────┐
          │  Mapping   │
          │ (映射关系)  │
          └─────┬──────┘
                │ N
                │
                │
         ┌──────▼─────┐
         │  Channel   │
         │  (通道)     │
         └────────────┘
```

---

## Tag 命名规范

### 普通数据点 Tag 格式
```
{DeviceCode}.{TagName}
```

**示例**：
- `SIM_DEVICE_001.Temperature01`
- `SIM_POWER.VoltageA`
- `SIM_CHILLER.ChilledWaterSupply`

### 虚拟数据点 Tag 格式
```
{DeviceCode}.Virtual.{VirtualTagName}
```

**示例**：
- `SIM_DEVICE_001.Virtual.AvgTemp`
- `SIM_POWER.Virtual.TotalPower`
- `SIM_CHILLER.Virtual.CoolingPower`

### Tag 特点

1. **全局唯一**：通过数据库唯一索引保证
2. **层级清晰**：`设备码。类型。点名` 结构
3. **便于解析**：可通过 `.` 分割快速定位设备和数据点
4. **表达式友好**：在虚拟点表达式中可直接引用

---

## 数据关系详解

### 1. Device ↔ DataPoint（一对多）

一个设备可以包含多个数据点。

```csharp
// Device 实体
public ICollection<DataPoint> DataPoints { get; set; }

// DataPoint 实体
public int DeviceId { get; set; }
public Device? Device { get; set; }
```

**级联规则**：删除设备时，级联删除其下所有数据点。

### 2. Device ↔ VirtualDataPoint（一对多）

一个设备可以包含多个虚拟数据点。

```csharp
// VirtualDataPoint 实体
public int DeviceId { get; set; }
public Device? Device { get; set; }
```

**级联规则**：删除设备时，级联删除其下所有虚拟数据点。

### 3. DataPoint ↔ Channel（多对多，通过 Mapping 表）

一个数据点可以映射到多个通道，一个通道也可以包含多个数据点。

```csharp
// DataPoint 实体
public ICollection<ChannelDataPointMapping> ChannelMappings { get; set; }

// Channel 实体
public ICollection<ChannelDataPointMapping> DataPointMappings { get; set; }

// Mapping 实体
public int? DataPointId { get; set; }
public int ChannelId { get; set; }
public string? DataPointTag { get; set; }  // Tag 快照
```

### 4. VirtualDataPoint ↔ Channel（多对多，通过 Mapping 表）

虚拟数据点同样可以映射到多个通道。

```csharp
// VirtualDataPoint 实体
public ICollection<ChannelDataPointMapping> ChannelMappings { get; set; }

// Mapping 实体
public int? VirtualDataPointId { get; set; }
public VirtualDataPoint? VirtualDataPoint { get; set; }
```

---

## 数据流转示例

### 场景：采集并上报温度数据

```
1. 设备层
   ┌─────────────────┐
   │ Device: SIM_DEVICE_001 │
   └────────┬────────┘
            │
   2. 数据点层
            ▼
   ┌─────────────────────────┐
   │ DataPoint               │
   │ Tag: SIM_DEVICE_001.Temperature01 │
   │ Address: 40001          │
   └────────┬────────────────┘
            │
   3. 映射关系层
            ▼
   ┌─────────────────────────┐
   │ ChannelDataPointMapping │
   │ DataPointTag: SIM_DEVICE_001.Temperature01 │
   │ ChannelId: 1 (HTTP)     │
   │ ChannelId: 2 (WS)       │
   └────────┬────────────────┘
            │
   4. 通道层
            ▼
   ┌─────────────────┐
   │ Channel         │
   │ Code: HTTP_SERVER│
   │ Code: WS_PUSH   │
   └─────────────────┘
```

### 虚拟数据点计算流程

```
1. 依赖数据点采集完成
   SIM_DEVICE_001.Temperature01 = 25.5
   SIM_DEVICE_001.Temperature02 = 26.3
   SIM_DEVICE_001.Temperature03 = 24.8

2. 触发虚拟点计算
   Expression: Avg(SIM_DEVICE_001.Temperature01, 
                   SIM_DEVICE_001.Temperature02, 
                   SIM_DEVICE_001.Temperature03)

3. 计算结果
   VirtualDataPoint: SIM_DEVICE_001.Virtual.AvgTemp
   LastValue: 25.53

4. 通过映射通道上报
   → HTTP_SERVER
   → WS_PUSH
```

---

## 种子数据说明

### 测试设备列表

| 设备编码 | 设备名称 | 描述 | 数据点数 |
|----------|----------|------|----------|
| SIM_DEVICE_001 | 模拟设备 001 | 温度压力模拟设备 | 6 |
| SIM_DEVICE_002 | 模拟设备 002 | 液位流量模拟设备 | 6 |
| SIM_DEVICE_003 | 模拟设备 003 | 环境监测模拟设备 | 8 |
| SIM_PROD_LINE | 产线模拟设备 | 产线监测模拟设备 | 6 |
| SIM_POWER | 动力站模拟设备 | 动力站监测模拟设备 | 9 |
| SIM_CHILLER | 冷站模拟设备 | 冷站系统模拟设备 | 8 |

### 虚拟数据点列表

| 所属设备 | 虚拟点 Tag | 描述 | 计算类型 |
|----------|-----------|------|----------|
| SIM_DEVICE_001 | SIM_DEVICE_001.Virtual.AvgTemp | 温度平均值 | Average |
| SIM_DEVICE_001 | SIM_DEVICE_001.Virtual.TempPressProd | 温压积 | Custom |
| SIM_DEVICE_001 | SIM_DEVICE_001.Virtual.TempDiff | 温差 | Custom |
| SIM_DEVICE_002 | SIM_DEVICE_002.Virtual.FlowDiff | 流量差 | Custom |
| SIM_DEVICE_002 | SIM_DEVICE_002.Virtual.AvgLevel | 平均液位 | Average |
| SIM_DEVICE_003 | SIM_DEVICE_003.Virtual.AQI | 空气质量指数 | Average |
| SIM_DEVICE_003 | SIM_DEVICE_003.Virtual.ComfortIndex | 舒适度指数 | Custom |
| SIM_PROD_LINE | SIM_PROD_LINE.Virtual.AvgTemp | 平均温度 | Average |
| SIM_PROD_LINE | SIM_PROD_LINE.Virtual.Efficiency | 能效比 | Custom |
| SIM_POWER | SIM_POWER.Virtual.AvgVoltage | 三相平均电压 | Average |
| SIM_POWER | SIM_POWER.Virtual.AvgCurrent | 三相平均电流 | Average |
| SIM_POWER | SIM_POWER.Virtual.TotalPower | 总功率 | Custom |
| SIM_CHILLER | SIM_CHILLER.Virtual.TempDiff | 供回水温差 | Custom |
| SIM_CHILLER | SIM_CHILLER.Virtual.CoolingPower | 瞬时冷量 | Custom |
| SIM_CHILLER | SIM_CHILLER.Virtual.AvgWaterTemp | 平均水温 | Average |

### 发送通道列表

| 通道编码 | 通道名称 | 协议类型 | 描述 |
|----------|----------|----------|------|
| HTTP_SERVER | HTTP 数据接口 | Http | 提供 HTTP 数据接口 |
| WS_PUSH | WebSocket 推送 | WebSocket | WebSocket 实时推送 |

**映射关系**：所有数据点和虚拟数据点均映射到两个通道。

---

## 关键代码位置

### 实体定义
- `EdgeGateway.Domain.Entities/Device.cs`
- `EdgeGateway.Domain.Entities/DataPoint.cs`
- `EdgeGateway.Domain.Entities/VirtualDataPoint.cs`
- `EdgeGateway.Domain.Entities/Channel.cs`
- `EdgeGateway.Domain.Entities/ChannelDataPointMapping.cs`

### 数据库上下文
- `EdgeGateway.Infrastructure.Data/GatewayDbContext.cs`

### 种子数据
- `EdgeGateway.Host/DatabaseSeeder.cs`

---

## 常见问题

### Q1: Tag 为什么要全局唯一？
**A**: 全局唯一的 Tag 便于：
- 在表达式中直接引用任意数据点
- 跨设备数据聚合和计算
- 简化数据路由和分发逻辑
- 避免命名冲突

### Q2: 为什么需要 DataPointTag 快照字段？
**A**: 
- 即使原始数据点被删除，映射记录仍可保留 Tag 信息
- 便于历史数据追溯和审计
- 减少关联查询，提高读取性能

### Q3: 如何保证数据一致性？
**A**: 
- 使用外键约束和级联删除
- Tag 唯一索引防止重复
- 检查约束确保 DataPointId 或 VirtualDataPointId 至少一个有效

---

## 更新日志

| 日期 | 版本 | 说明 |
|------|------|------|
| 2026-03-05 | 1.0 | 初始版本，更新 Tag 格式为全局唯一 |
