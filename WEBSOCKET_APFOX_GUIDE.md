# WebSocket 服务端 - Apifox 连接指南

## 概述

EdgeGateway 现已支持 **WebSocket 服务端模式**，可以作为 WebSocket 服务器等待客户端连接，并将采集的数据推送给订阅的客户端。

---

## 快速开始

### 1. 启动网关服务

```bash
cd EdgeGateway.WebApi
dotnet run
```

服务默认监听：`http://0.0.0.0:5000`

WebSocket 端点：`ws://localhost:5000/ws`

---

### 2. 使用 Apifox 连接 WebSocket

#### 步骤 1：创建 WebSocket 请求

1. 打开 Apifox
2. 点击 **新建 API** → 选择 **WebSocket** 协议
3. 填写请求信息：
   - **请求地址**: `ws://localhost:5000/ws`
   - **请求方法**: `CONNECT`

#### 步骤 2：配置连接参数（可选）

**方式一：使用 Query 参数指定订阅主题**
```
ws://localhost:5000/ws?topic=device/data
```

**方式二：使用 Header 指定订阅主题**
| 参数名 | 值 |
|--------|-----|
| `X-Subscribe-Topic` | `device/data` |

在 Apifox 的 **Headers** 标签页中添加。

#### 步骤 3：连接服务器

点击 **连接** 按钮，成功后会收到欢迎消息：

```json
{
  "type": "welcome",
  "clientId": "a1b2c3d4",
  "timestamp": 1740844800000,
  "message": "欢迎连接到 EdgeGateway WebSocket 服务器"
}
```

---

### 3. 客户端消息类型

连接成功后，你可以发送以下类型的消息：

#### 3.1 心跳 Ping

**发送：**
```json
{
  "type": "ping"
}
```

**响应：**
```json
{
  "type": "pong",
  "timestamp": 1740844800000
}
```

#### 3.2 订阅主题

**发送：**
```json
{
  "type": "subscribe",
  "topic": "device/data"
}
```

**响应：**
```json
{
  "type": "ack",
  "action": "subscribe",
  "topic": "device/data"
}
```

#### 3.3 取消订阅

**发送：**
```json
{
  "type": "unsubscribe"
}
```

**响应：**
```json
{
  "type": "ack",
  "action": "unsubscribe"
}
```

---

### 4. 接收服务器推送数据

当网关采集到数据时，会自动推送给订阅的客户端：

```json
{
  "type": "data",
  "timestamp": 1740844800000,
  "channelCode": "channel_001",
  "channelName": "测试通道",
  "data": [
    {
      "name": "温度",
      "value": 25.5,
      "quality": "Good"
    },
    {
      "name": "湿度",
      "value": 60.2,
      "quality": "Good"
    }
  ]
}
```

---

## 通道配置

在网关中配置 WebSocket 发送通道时，使用以下配置：

### 前端管理界面配置

1. 访问网关管理前端（`http://localhost:5000`）
2. 进入 **发送通道** 页面
3. 点击 **新增通道**
4. 选择协议：`WebSocket`
5. 填写配置：
   - **通道名称**: 如 "WebSocket 推送通道"
   - **通道编码**: 如 "websocket_001"
   - **Endpoint**: `/ws`
   - **配置 JSON**:
     ```json
     {
       "subscribeTopic": "device/data",
       "heartbeatInterval": 30000
     }
     ```

### ConfigJson 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `subscribeTopic` | string | `device/data` | 客户端订阅的主题 |
| `heartbeatInterval` | int | `30000` | 心跳间隔（毫秒） |

---

## 完整测试流程

1. **启动网关**
   ```bash
   cd EdgeGateway.WebApi
   dotnet run
   ```

2. **打开 Apifox**，创建 WebSocket 请求

3. **连接服务器**
   - 地址：`ws://localhost:5000/ws`
   - 点击 **连接**

4. **订阅主题**（可选，连接时已自动订阅）
   ```json
   { "type": "subscribe", "topic": "device/data" }
   ```

5. **接收数据**
   - 网关会每 2 秒采集一次数据（如果配置了模拟设备）
   - 数据会自动推送到 Apifox

6. **查看日志**
   - 网关控制台会输出连接和数据推送日志
   - Apifox 会显示收发的所有消息

---

## 常见问题

### Q: 连接失败怎么办？

A: 检查以下几点：
1. 确保网关服务已启动
2. 检查端口 5000 是否被占用
3. 确认防火墙允许连接

### Q: 收不到数据推送？

A: 检查：
1. 是否已订阅正确的主题
2. 网关中是否配置了启用的 WebSocket 通道
3. 通道是否绑定了数据点

### Q: 如何查看当前在线客户端？

A: 查看网关控制台日志，会显示连接和断开的客户端数量。

---

## 技术架构

```
┌─────────────────┐
│  Apifox 客户端   │
│  (或其他 WS 客户端)│
└────────┬────────┘
         │ WebSocket
         │ ws://localhost:5000/ws
         ▼
┌─────────────────────────────────┐
│   EdgeGateway WebSocket Server  │
│  ┌───────────────────────────┐  │
│  │ WebSocketServerMiddleware │  │
│  └─────────────┬─────────────┘  │
│                │                │
│  ┌─────────────▼─────────────┐  │
│  │ WebSocketConnectionManager│  │
│  │  - 管理所有客户端连接      │  │
│  │  - 按主题推送消息          │  │
│  └─────────────┬─────────────┘  │
│                │                │
│  ┌─────────────▼─────────────┐  │
│  │  WebSocketSendStrategy    │  │
│  │  - 推送采集数据            │  │
│  └───────────────────────────┘  │
└─────────────────────────────────┘
```

---

## 下一步

- 配置真实的采集设备和数据点
- 设置多个 WebSocket 通道，推送不同类型的数据
- 实现客户端认证机制
- 扩展更多消息类型（命令、配置更新等）
