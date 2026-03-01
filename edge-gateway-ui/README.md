# EdgeGateway 前端 UI

基于 **Vue 3 + Element Plus + Vite** 构建的边缘采集网关管理系统前端。

## 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| Vue 3 | ^3.4 | 框架（Composition API + `<script setup>`）|
| Vite  | ^5.1 | 构建工具 |
| Element Plus | ^2.6 | UI 组件库 |
| Vue Router | ^4.3 | 路由管理 |
| Pinia | ^2.1 | 状态管理 |
| Axios | ^1.6 | HTTP 请求 |

## 快速启动

```bash
cd edge-gateway-ui
npm install
npm run dev       # 开发模式 → http://localhost:3000
npm run build     # 生产构建
npm run preview   # 预览生产包
```

> 开发时 `/api` 请求自动代理到 `http://localhost:5000`（后端 WebApi 地址），见 `vite.config.js`

## 页面结构

```
/dashboard            总览         - 网关状态、设备/通道统计卡片
/devices              设备管理     - 设备列表 CRUD、启用/禁用
/devices/:id/datapoints  数据点管理 - 指定设备下的数据点 CRUD
/channels             发送通道     - 通道列表卡片 + 新增
/channels/:id/mappings  映射管理  - 绑定/解绑数据点到通道
```

## 目录结构

```
src/
├── api/
│   ├── request.js        # Axios 封装（统一错误处理）
│   ├── device.js         # 设备 / 数据点 API
│   ├── channel.js        # 通道 / 映射 API
│   ├── gateway.js        # 网关状态 API
│   └── constants.js      # 枚举常量 + 辅助函数
├── layouts/
│   └── MainLayout.vue    # 主布局（侧边栏 + 顶栏）
├── router/
│   └── index.js          # 路由配置
├── styles/
│   └── global.css        # 全局样式 + CSS 变量 + 暗色主题
└── views/
    ├── DashboardView.vue    # 总览页
    ├── DevicesView.vue      # 设备管理页
    ├── DataPointsView.vue   # 数据点管理页
    ├── ChannelsView.vue     # 发送通道页
    └── MappingsView.vue     # 数据点映射页
```

## 设计风格

工业监控风格（Industrial/SCADA + Modern Web）：
- **配色**：深海军蓝底 `#0a0e1a` + 青色数据高亮 `#38dcc4`
- **字体**：UI 用 Syne（几何感强），数值/代码用 JetBrains Mono
- **状态**：脉冲圆点指示在线状态，徽标区分 Good/Bad/Warn
