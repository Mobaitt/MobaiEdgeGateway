# EdgeGateway API 参考文档

> 完整的 API 接口说明，包含请求示例和响应格式

---

## 📋 目录

1. [通用说明](#通用说明)
2. [设备管理 API](#设备管理-api)
3. [通道管理 API](#通道管理-api)
4. [规则管理 API](#规则管理-api)
5. [虚拟节点 API](#虚拟节点-api)
6. [网关状态 API](#网关状态-api)
7. [枚举选项 API](#枚举选项-api)
8. [错误响应](#错误响应)

---

## 通用说明

### 基础 URL

```
开发环境：http://localhost:5000/api
生产环境：http://your-domain/api
```

### 认证方式

当前版本无需认证（可选扩展 JWT）

### 请求格式

```json
Content-Type: application/json
```

### 响应格式

**成功响应**：
```json
{
  "success": true,
  "data": { ... },
  "message": "操作成功",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

**失败响应**：
```json
{
  "success": false,
  "error": {
    "code": "DEVICE_NOT_FOUND",
    "message": "设备不存在"
  },
  "message": "操作失败",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 分页参数

```json
{
  "page": 1,
  "pageSize": 10
}
```

---

## 设备管理 API

### 获取所有设备

**请求**：
```http
GET /api/devices
```

**响应**：
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "车间 A-PLC01",
      "code": "PLC001",
      "description": "一号 PLC",
      "protocol": 1,
      "protocolName": "Modbus",
      "address": "192.168.1.100",
      "port": 502,
      "isEnabled": true,
      "pollingIntervalMs": 1000,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z"
    }
  ]
}
```

### 获取设备详情

**请求**：
```http
GET /api/devices/{id}
```

**响应**：
```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "车间 A-PLC01",
    "code": "PLC001",
    "protocol": 1,
    "address": "192.168.1.100",
    "port": 502,
    "isEnabled": true,
    "pollingIntervalMs": 1000,
    "dataPoints": [
      {
        "id": 1,
        "name": "温度",
        "tag": "PLC001.Temperature",
        "dataType": 6,
        "unit": "℃"
      }
    ]
  }
}
```

### 新增设备

**请求**：
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

**响应**：
```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "车间 A-PLC01",
    "code": "PLC001"
  },
  "message": "设备创建成功"
}
```

### 更新设备

**请求**：
```http
PUT /api/devices/{id}
Content-Type: application/json

{
  "name": "车间 A-PLC01",
  "code": "PLC001",
  "description": "一号 PLC（已更新）",
  "protocol": 1,
  "address": "192.168.1.100",
  "port": 502,
  "isEnabled": true,
  "pollingIntervalMs": 2000
}
```

### 删除设备

**请求**：
```http
DELETE /api/devices/{id}
```

**响应**：
```json
{
  "success": true,
  "message": "设备删除成功"
}
```

### 启用/禁用设备

**请求**：
```http
PATCH /api/devices/{id}/toggle
Content-Type: application/json

{
  "isEnabled": true
}
```

### 获取设备数据点

**请求**：
```http
GET /api/devices/{id}/datapoints
```

### 新增数据点

**请求**：
```http
POST /api/devices/{id}/datapoints
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
  "modbusByteOrder": 1,
  "registerLength": 1,
  "isEnabled": true
}
```

### 获取实时数据

**请求**：
```http
GET /api/devices/{id}/datapoints/realtime
```

**响应**：
```json
{
  "success": true,
  "data": [
    {
      "tag": "PLC001.Temperature",
      "value": 25.5,
      "unit": "℃",
      "quality": 0,
      "qualityName": "Good",
      "timestamp": "2024-01-01T00:00:00Z"
    }
  ]
}
```

---

## 通道管理 API

### 获取所有通道

**请求**：
```http
GET /api/channels
```

**响应**：
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "MQTT 上传",
      "code": "MQTT001",
      "protocol": 1,
      "protocolName": "MQTT",
      "endpoint": "tcp://localhost:1883",
      "mqttTopic": "edge/data",
      "mqttClientId": "gateway001",
      "isEnabled": true,
      "dataPointCount": 5
    }
  ]
}
```

### 新增通道

**请求**：
```http
POST /api/channels
Content-Type: application/json

{
  "name": "MQTT 上传",
  "code": "MQTT001",
  "protocol": 1,
  "endpoint": "tcp://localhost:1883",
  "mqttTopic": "edge/data",
  "mqttClientId": "gateway001",
  "mqttQos": 1,
  "isEnabled": true
}
```

### 更新通道

**请求**：
```http
PUT /api/channels/{id}
Content-Type: application/json

{
  "name": "MQTT 上传",
  "code": "MQTT001",
  "protocol": 1,
  "endpoint": "tcp://localhost:1883",
  "mqttTopic": "edge/data",
  "isEnabled": true
}
```

### 删除通道

**请求**：
```http
DELETE /api/channels/{id}
```

### 启用/停用通道

**请求**：
```http
PATCH /api/channels/{id}/toggle
Content-Type: application/json

{
  "isEnabled": true
}
```

### 绑定数据点

**请求**：
```http
POST /api/channels/{id}/bind-datapoints
Content-Type: application/json

{
  "dataPointIds": [1, 2, 3]
}
```

### 绑定虚拟数据点

**请求**：
```http
POST /api/channels/{id}/bind-virtual-datapoints
Content-Type: application/json

{
  "virtualDataPointIds": [1, 2]
}
```

### 获取映射关系

**请求**：
```http
GET /api/channels/{id}/mappings
```

**响应**：
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "channelId": 1,
      "dataPointId": 1,
      "dataPointTag": "PLC001.Temperature",
      "dataPointName": "温度",
      "isEnabled": true
    }
  ]
}
```

### 删除映射

**请求**：
```http
DELETE /api/channels/mappings/{id}
```

---

## 规则管理 API

### 获取所有规则

**请求**：
```http
GET /api/rules
```

**响应**：
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "温度限制",
      "ruleType": 0,
      "ruleTypeName": "Limit",
      "dataPointId": 1,
      "dataPointTag": "PLC001.Temperature",
      "priority": 100,
      "isEnabled": true,
      "ruleConfig": "{\"min\": 0, \"max\": 100}",
      "onFailure": 0
    }
  ]
}
```

### 获取规则详情

**请求**：
```http
GET /api/rules/{id}
```

### 创建规则

**请求**：
```http
POST /api/rules
Content-Type: application/json

{
  "name": "温度限制",
  "description": "限制温度范围",
  "ruleType": 0,
  "dataPointId": 1,
  "priority": 100,
  "isEnabled": true,
  "ruleConfig": {
    "type": "limit",
    "min": 0,
    "max": 100
  },
  "onFailure": 0
}
```

### 更新规则

**请求**：
```http
PUT /api/rules/{id}
Content-Type: application/json

{
  "name": "温度限制",
  "ruleType": 0,
  "priority": 100,
  "ruleConfig": {
    "type": "limit",
    "min": -10,
    "max": 150
  }
}
```

### 删除规则

**请求**：
```http
DELETE /api/rules/{id}
```

### 启用/禁用规则

**请求**：
```http
PATCH /api/rules/{id}/toggle
Content-Type: application/json

{
  "isEnabled": true
}
```

### 测试规则

**请求**：
```http
POST /api/rules/test
Content-Type: application/json

{
  "ruleConfig": {
    "type": "limit",
    "min": 0,
    "max": 100
  },
  "inputValue": 50,
  "dataType": 6
}
```

**响应**：
```json
{
  "success": true,
  "data": {
    "outputValue": 50,
    "executionTimeMs": 2.5
  }
}
```

---

## 虚拟节点 API

### 获取所有虚拟数据点

**请求**：
```http
GET /api/virtual-nodes/points
```

**响应**：
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "平均温度",
      "tag": "AVG.Temp",
      "expression": "Average(Temp1, Temp2, Temp3)",
      "calculationType": 1,
      "calculationTypeName": "Average",
      "dataType": 6,
      "unit": "℃",
      "dependencyTags": ["PLC001.Temp1", "PLC001.Temp2", "PLC001.Temp3"],
      "isEnabled": true,
      "lastValue": 25.5,
      "lastCalculationTime": "2024-01-01T00:00:00Z"
    }
  ]
}
```

### 创建虚拟数据点

**请求**：
```http
POST /api/virtual-nodes/points
Content-Type: application/json

{
  "name": "平均温度",
  "tag": "AVG.Temp",
  "expression": "Average(Temp1, Temp2, Temp3)",
  "calculationType": 1,
  "dataType": 6,
  "unit": "℃",
  "deviceId": 1,
  "dependencyTags": ["PLC001.Temp1", "PLC001.Temp2", "PLC001.Temp3"],
  "isEnabled": true
}
```

### 更新虚拟数据点

**请求**：
```http
PUT /api/virtual-nodes/points/{id}
Content-Type: application/json

{
  "name": "平均温度",
  "expression": "Average(Temp1, Temp2, Temp3, Temp4)",
  "dependencyTags": ["PLC001.Temp1", "PLC001.Temp2", "PLC001.Temp3", "PLC001.Temp4"]
}
```

### 删除虚拟数据点

**请求**：
```http
DELETE /api/virtual-nodes/points/{id}
```

### 触发计算

**请求**：
```http
POST /api/virtual-nodes/points/{id}/calculate
```

**响应**：
```json
{
  "success": true,
  "data": {
    "value": 25.5,
    "calculationTime": "2024-01-01T00:00:00Z"
  }
}
```

### 批量计算

**请求**：
```http
POST /api/virtual-nodes/points/calculate-all
```

### 解析依赖

**请求**：
```http
POST /api/virtual-nodes/parse-dependencies
Content-Type: application/json

{
  "expression": "Average(Temp1, Temp2) + Max(Pressure1, Pressure2)"
}
```

**响应**：
```json
{
  "success": true,
  "data": {
    "dependencies": ["Temp1", "Temp2", "Pressure1", "Pressure2"]
  }
}
```

---

## 网关状态 API

### 获取网关状态

**请求**：
```http
GET /api/gateway/status
```

**响应**：
```json
{
  "success": true,
  "data": {
    "status": "Running",
    "uptime": "01:23:45",
    "deviceCount": 5,
    "activeDeviceCount": 3,
    "dataPointCount": 50,
    "channelCount": 3,
    "activeChannelCount": 2,
    "collectionRate": 99.5,
    "sendRate": 98.2,
    "lastCollectionTime": "2024-01-01T00:00:00Z",
    "lastSendTime": "2024-01-01T00:00:00Z"
  }
}
```

### 健康检查

**请求**：
```http
GET /api/gateway/health
```

**响应**：
```json
{
  "success": true,
  "data": {
    "status": "Healthy",
    "database": "Connected",
    "memory": 1024000,
    "cpu": 15.5
  }
}
```

### 停止设备采集

**请求**：
```http
POST /api/gateway/devices/{id}/stop
```

---

## 枚举选项 API

### 获取采集协议选项

**请求**：
```http
GET /api/enums/collection-protocols
```

**响应**：
```json
{
  "success": true,
  "data": [
    { "value": 0, "label": "Simulator" },
    { "value": 1, "label": "Modbus" },
    { "value": 2, "label": "OpcUa" },
    { "value": 3, "label": "Virtual" },
    { "value": 4, "label": "S7" }
  ]
}
```

### 获取发送协议选项

**请求**：
```http
GET /api/enums/send-protocols
```

### 获取数据类型选项

**请求**：
```http
GET /api/enums/data-value-types
```

### 获取字节序选项

**请求**：
```http
GET /api/enums/modbus-byte-orders
```

### 获取规则类型选项

**请求**：
```http
GET /api/enums/rule-types
```

### 获取计算类型选项

**请求**：
```http
GET /api/enums/calculation-types
```

---

## 错误响应

### 错误码列表

| 错误码 | 说明 | HTTP 状态码 |
|--------|------|-------------|
| `SUCCESS` | 操作成功 | 200 |
| `BAD_REQUEST` | 请求参数错误 | 400 |
| `UNAUTHORIZED` | 未授权 | 401 |
| `FORBIDDEN` | 禁止访问 | 403 |
| `NOT_FOUND` | 资源不存在 | 404 |
| `CONFLICT` | 资源冲突 | 409 |
| `INTERNAL_ERROR` | 服务器内部错误 | 500 |

### 错误响应格式

```json
{
  "success": false,
  "error": {
    "code": "DEVICE_NOT_FOUND",
    "message": "设备不存在",
    "details": {
      "deviceId": 1
    }
  },
  "message": "操作失败",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

---

## 相关文档

- [DEVELOPER_GUIDE.md](./DEVELOPER_GUIDE.md) - 开发者指南
- [ARCHITECTURE.md](./ARCHITECTURE.md) - 架构设计文档
