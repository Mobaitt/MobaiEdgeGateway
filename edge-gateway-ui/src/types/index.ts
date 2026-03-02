/**
 * 全局共享类型定义，与后端模型、API 响应保持一致
 */

/** 设备 */
export interface DeviceItem {
  id: number
  name: string
  code: string
  description?: string
  protocol?: number
  protocolValue: number
  address: string
  port?: number | null
  pollingIntervalMs: number
  isEnabled: boolean
  dataPointCount: number
  createdAt?: string
}

/** 数据点 */
export interface DataPointItem {
  id: number
  name: string
  tag: string
  description?: string
  address: string
  dataType: string | number
  dataTypeValue?: number
  unit?: string
  isEnabled: boolean
  createdAt?: string
  modbusSlaveId?: number
  modbusFunctionCode?: number
  modbusByteOrder?: number
  registerLength?: number
}

/** 通道映射项 */
export interface MappingItem {
  id: number
  dataPointId: number
  dataPointTag: string
  dataPointName: string
  aliasName?: string
  isEnabled: boolean
  createdAt?: string
}

/** 发送通道 */
export interface ChannelItem {
  id: number
  name: string
  code: string
  description?: string
  protocol: string
  protocolValue: number
  endpoint: string
  mqttTopic?: string
  mqttClientId?: string
  mqttUsername?: string
  mqttPassword?: string
  mqttQos?: number
  httpMethod?: string
  httpToken?: string
  httpTimeout?: number
  httpMode?: string
  wsSubscribeTopic?: string
  wsHeartbeatInterval?: number
  fileFormat?: string
  filePath?: string
  mappedDataPointCount: number
  isEnabled: boolean
  createdAt?: string
}

/** 网关状态（总览） */
export interface GatewayStatus {
  isRunning?: boolean
  totalDevices?: number
  enabledDevices?: number
  totalChannels?: number
  enabledChannels?: number
  totalDataPoints?: number
}

/** 设备树节点（映射页用） */
export interface DeviceNode {
  id: number
  name: string
  code: string
  dataPoints: DataPointItem[]
}

/** 实时数据点 */
export interface RealtimeDataItem {
  dataPointId: number
  value: unknown
  quality: string
  timestamp: string
}
