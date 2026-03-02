# 规则检验与虚拟节点功能说明

## 概述

EdgeGateway 支持两大核心功能：
1. **规则检验引擎** - 支持数据采集时的限制、转换、校验和计算
2. **虚拟节点引擎** - 支持通过表达式计算派生数据

---

## 一、规则检验功能

### 1.1 规则类型

#### Limit（限制规则）
- **最大采集频率**：限制设备采集的最大频率
- **数据量限制**：限制单次采集的数据点数量
- **并发限制**：限制同时采集的设备数量
- **数值范围**：限制数据值的最大/最小值

#### Transform（转换规则）
- **线性变换**：`y = kx + b`（Scale 和 Offset 参数）
- **多项式变换**：`y = a*x^n + b*x^(n-1) + ... + c`
- **查表转换**：输入输出映射表，支持线性插值
- **公式计算**：使用 NCalc 表达式引擎
- **单位换算**：支持常见单位转换（℃↔℉, MPa↔bar）

#### Validation（校验规则）
- **阈值校验**：检查数据是否在 MinValue 和 MaxValue 范围内
- **变化率校验**：检查单位时间内数据变化是否超过 MaxRateOfChange
- **死区校验**：变化小于 DeadBand 时不更新数据
- **合理性校验**：使用 NCalc 表达式进行自定义校验
- **固定值校验**：检查数据是否等于指定值（允许 FixedValueTolerance 容差）

#### Calculation（计算规则）
- 支持多数据点组合计算
- 预留接口，实际计算由虚拟节点引擎处理

### 1.2 规则配置示例

#### 阈值校验规则
```json
{
  "ValidationType": 1,
  "MinValue": -50,
  "MaxValue": 150
}
```

#### 线性变换规则
```json
{
  "TransformType": 1,
  "Scale": 1.8,
  "Offset": 32
}
```

#### 变化率校验规则
```json
{
  "ValidationType": 2,
  "MaxRateOfChange": 10.5
}
```

### 1.3 规则失败处理方式

- **Pass**：记录警告日志，放行数据
- **Reject**：拒绝数据，不向下传递
- **DefaultValue**：使用配置的默认值

### 1.4 API 接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/rules` | 获取所有规则 |
| GET | `/api/rules/{id}` | 获取指定规则 |
| GET | `/api/rules/datapoint/{dataPointId}` | 获取数据点的所有规则 |
| GET | `/api/rules/device/{deviceId}` | 获取设备的所有规则 |
| GET | `/api/rules/global` | 获取全局规则 |
| POST | `/api/rules` | 创建规则 |
| PUT | `/api/rules/{id}` | 更新规则 |
| DELETE | `/api/rules/{id}` | 删除规则 |
| PATCH | `/api/rules/{id}/toggle` | 启用/禁用规则 |
| POST | `/api/rules/test` | 测试规则 |

---

## 二、虚拟节点功能

### 2.1 核心概念

#### 虚拟数据点 (VirtualDataPoint)
- 通过表达式计算得出的数据点
- 依赖于真实数据点或其他虚拟数据点
- **虚拟节点依附于普通设备**，与设备关联
- 可以像普通数据点一样被通道选择和发送
- 支持多种计算类型

### 2.2 计算类型

| 类型 | 说明 | 示例 |
|------|------|------|
| Custom | 自定义表达式 | `(Temp1 + Temp2) / 2` |
| Sum | 求和 | `Point1 + Point2 + Point3` |
| Average | 平均值 | `(P1 + P2 + P3) / 3` |
| Max | 最大值 | `Max(Temp1, Temp2, Temp3)` |
| Min | 最小值 | `Min(Pressure1, Pressure2)` |
| Count | 计数 | 非空数据点数量 |
| WeightedAverage | 加权平均 | `P1*0.3 + P2*0.7` |

### 2.3 表达式语法

支持以下操作：

#### 四则运算
```
Point1 + Point2
Point1 * 2 + Point2 / 3
```

#### 数学函数
```
Math.Abs(Value)
Math.Sqrt(Power)
Math.Sin(Angle)
Math.Cos(Angle)
Math.Pow(Base, Exponent)
Math.Log(Value)
Math.Round(Value, 2)
```

#### 聚合函数
```
Max(Point1, Point2, Point3)
Min(Point1, Point2)
Avg(Temp1, Temp2, Temp3)
Sum(Value1, Value2, Value3)
```

#### 条件表达式
```
Temperature > 100 ? 1 : 0
Pressure > 50 && Temperature < 80 ? "Normal" : "Alert"
```

### 2.4 依赖驱动计算

虚拟节点支持两种计算模式：

1. **依赖驱动**（默认）
   - 当依赖的数据点更新时自动触发计算
   - 适合实时性要求高的场景

2. **手动/定时计算**
   - 通过 API 手动触发计算
   - 适合统计类计算

### 2.5 API 接口

#### 虚拟数据点管理

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/virtualnodes/points` | 获取所有虚拟数据点 |
| GET | `/api/virtualnodes/devices/{deviceId}/points` | 获取指定设备的虚拟数据点 |
| GET | `/api/virtualnodes/points/{id}` | 获取指定虚拟数据点 |
| POST | `/api/virtualnodes/points` | 创建虚拟数据点 |
| PUT | `/api/virtualnodes/points/{id}` | 更新虚拟数据点 |
| DELETE | `/api/virtualnodes/points/{id}` | 删除虚拟数据点 |
| POST | `/api/virtualnodes/points/{id}/calculate` | 触发虚拟数据点计算 |
| POST | `/api/virtualnodes/devices/{deviceId}/calculate` | 计算设备下所有虚拟数据点 |
| POST | `/api/virtualnodes/points/calculate-all` | 计算所有启用的虚拟数据点 |
| POST | `/api/virtualnodes/parse-dependencies` | 解析表达式依赖 |

---

## 三、使用示例

### 3.1 创建温度范围校验规则

```bash
POST /api/rules
Content-Type: application/json

{
  "dataPointId": 1,
  "name": "温度范围校验",
  "description": "确保温度值在合理范围内",
  "ruleType": 2,
  "isEnabled": true,
  "priority": 10,
  "ruleConfig": "{\"ValidationType\":1,\"MinValue\":0,\"MaxValue\":100}",
  "onFailure": 0
}
```

### 3.2 创建虚拟数据点（平均温度）

```bash
POST /api/virtualnodes/points
Content-Type: application/json

{
  "deviceId": 1,
  "name": "平均温度",
  "tag": "DEV001.Virtual.AvgTemp",
  "description": "三个温度传感器的平均值",
  "expression": "Avg(DEV001.Temp1, DEV001.Temp2, DEV001.Temp3)",
  "calculationType": 0,
  "dataType": 4,
  "unit": "℃",
  "isEnabled": true
}
```

### 3.3 解析表达式依赖

```bash
POST /api/virtualnodes/parse-dependencies
Content-Type: application/json

"DEV001.Temp1 + DEV001.Temp2 * 2"

# 响应
["DEV001.Temp1", "DEV001.Temp2"]
```

---

## 四、数据库表结构

### 表

1. **DataPointRules** - 数据点规则表
   - Id, DataPointId, DeviceId, Name, RuleType, RuleConfig, Priority, etc.

2. **VirtualDataPoints** - 虚拟数据点表
   - Id, DeviceId, Name, Tag, Expression, CalculationType, DependencyTags, etc.
   - **虚拟数据点通过 DeviceId 外键关联到普通设备**

3. **ChannelDataPointMappings** - 通道数据点映射表
   - Id, ChannelId, DataPointId (可空), VirtualDataPointId (可空)
   - **支持普通数据点和虚拟数据点的映射**

---

## 五、数据处理流程

```
采集数据 → 规则引擎 → 虚拟节点引擎 → 发送服务
           ↓            ↓            ↓
        限制/转换/校验  计算派生数据   包含虚拟数据点
```

1. **采集阶段**：从设备读取原始数据
2. **规则处理**：依次执行 Limit → Transform → Validation 规则
3. **虚拟计算**：触发依赖此数据的虚拟节点计算，结果更新到快照
4. **数据发送**：将处理后的数据（包括虚拟数据点）通过通道发送

---

## 六、注意事项

1. **规则优先级**：数值越小优先级越高，规则按优先级顺序执行
2. **循环依赖**：虚拟节点不支持循环依赖，创建时会检测
3. **表达式语法**：使用 NCalc 表达式引擎，支持标准数学运算
4. **性能考虑**：大量虚拟节点时建议使用依赖驱动模式
5. **数据质量**：规则失败时数据质量标记为 Bad
6. **虚拟节点**：
   - 虚拟数据点依附于普通设备，通过 DeviceId 关联
   - Tag 建议格式：`{DeviceCode}.Virtual.{Name}`
   - 可以像普通数据点一样被通道选择和发送
   - 在快照中使用负 ID 存储，以区分于普通数据点

---

## 七、扩展开发

### 添加新的规则类型

1. 在 `RuleType` 枚举中添加新类型
2. 在 `RuleEngine.cs` 的 `ExecuteSingleRuleAsync` 方法中添加处理逻辑
3. 创建对应的规则配置类

### 添加新的计算类型

1. 在 `CalculationType` 枚举中添加新类型
2. 在 `VirtualNodeEngine.cs` 的 `ExecuteCalculation` 方法中添加计算逻辑
3. 实现对应的计算方法

---

## 九、前端界面

### 9.1 规则管理界面

访问 `/rules` 路径进入规则管理界面。

**功能特性：**
- ✅ 规则列表展示（支持按类型筛选）
- ✅ 创建/编辑规则（表单验证）
- ✅ 规则启用/禁用切换
- ✅ 规则测试功能
- ✅ 配置帮助（JSON 格式示例）
- ✅ 设备和数据点选择器

### 9.2 虚拟节点界面

访问 `/virtual-nodes` 路径进入虚拟节点管理界面。

**功能特性：**
- ✅ 设备列表（左侧）
- ✅ 虚拟数据点列表（右侧，按设备组织）
- ✅ 创建/编辑虚拟数据点
- ✅ 表达式语法帮助
- ✅ 依赖解析功能
- ✅ 立即计算按钮（单个/设备/全部）
- ✅ 计算结果展示（含依赖值）

**界面布局：**
```
┌──────────────────┬────────────────────────────────┐
│ 设备             │ 虚拟数据点                     │
│ ┌──────────────┐ │ ┌────────────────────────────┐ │
│ │ 设备列表     │ │ │ 数据点列表                 │ │
│ │              │ │ │ [新建]                     │ │
│ └──────────────┘ │ └────────────────────────────┘ │
└──────────────────┴────────────────────────────────┘
┌────────────────────────────────────────────────────┐
│ 计算结果                      [计算设备] [计算全部]│
│ ┌────────────────────────────────────────────────┐ │
│ │ 数据点│结果│质量│时间│依赖值                 │ │
│ └────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────┘
```

### 9.3 通道映射

在通道管理界面的数据点映射中：
- ✅ 可以选择普通数据点
- ✅ 可以选择虚拟数据点
- ✅ 虚拟数据点与普通数据点一样参与发送

### 9.4 前端 API 配置

前端通过 `src/api/request.ts` 配置 API 基础地址：

```typescript
// 开发环境默认代理到 http://localhost:5000
// 生产环境需要修改为实际后端地址
const baseURL = import.meta.env.VITE_API_BASE_URL || '/api'
```

### 9.5 路由配置

在 `src/router/index.ts` 中已添加两个新路由：

```typescript
{
  path: 'rules',
  name: 'Rules',
  component: () => import('@/views/RulesView.vue'),
  meta: { title: '规则管理', icon: 'Setting' }
},
{
  path: 'virtual-nodes',
  name: 'VirtualNodes',
  component: () => import('@/views/VirtualNodesView.vue'),
  meta: { title: '虚拟节点', icon: 'Cpu' }
}
```

菜单会自动从路由生成，无需额外配置。

---

## 十、依赖包

- **NCalcSync** (5.4.2) - 表达式计算引擎
- **Newtonsoft.Json** (13.0.3) - JSON 序列化
