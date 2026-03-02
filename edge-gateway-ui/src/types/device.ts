import type { CollectionProtocol } from './enums'

/**
 * 设备实体
 */
export interface Device {
  id: number
  name: string
  code: string
  description: string | null
  protocol: CollectionProtocol
  address: string
  port: number | null
  isEnabled: boolean
  pollingIntervalMs: number
  createdAt: string
  updatedAt: string
  dataPoints?: DataPoint[]
}

/**
 * 数据点实体
 */
export interface DataPoint {
  id: number
  deviceId: number
  name: string
  tag: string
  description: string | null
  address: string
  dataType: number
  unit: string | null
  modbusSlaveId: number | null
  modbusFunctionCode: number | null
  modbusByteOrder: number | null
  registerLength: number
  isEnabled: boolean
  createdAt: string
}

/**
 * 创建设备请求
 */
export interface CreateDeviceRequest {
  name: string
  code: string
  description?: string
  protocol: CollectionProtocol
  address: string
  port?: number
  pollingIntervalMs?: number
  isEnabled?: boolean
}

/**
 * 更新设备请求
 */
export interface UpdateDeviceRequest {
  id: number
  name: string
  code: string
  description?: string
  protocol: CollectionProtocol
  address: string
  port?: number
  isEnabled: boolean
  pollingIntervalMs: number
}

/**
 * 创建数据点请求
 */
export interface CreateDataPointRequest {
  deviceId: number
  name: string
  tag: string
  description?: string
  address: string
  dataType: number
  unit?: string
  modbusSlaveId?: number
  modbusFunctionCode?: number
  modbusByteOrder?: number
  registerLength?: number
  isEnabled?: boolean
}

/**
 * 更新数据点请求
 */
export interface UpdateDataPointRequest {
  id: number
  name: string
  tag: string
  description?: string
  address: string
  dataType: number
  unit?: string
  modbusSlaveId?: number
  modbusFunctionCode?: number
  modbusByteOrder?: number
  registerLength?: number
  isEnabled: boolean
}
