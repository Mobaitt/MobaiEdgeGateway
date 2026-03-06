import type { CalculationType, DataValueType } from './enums'

/**
 * 虚拟数据点
 * 虚拟节点依附于普通设备，可以像普通数据点一样被管理和发送
 */
export interface VirtualDataPoint {
  id: number
  deviceId: number
  deviceName?: string | null
  name: string
  tag: string
  description?: string
  expression: string
  calculationType: CalculationType
  dataType: DataValueType
  unit?: string | null
  isEnabled: boolean
  dependencyTags: string[]
  lastCalculationTime?: string | null
  lastValue?: any | null
  createdAt?: string
}

/**
 * 创建虚拟数据点请求
 */
export interface CreateVirtualDataPointRequest {
  deviceId: number
  name: string
  tag: string
  description?: string
  expression: string
  calculationType: CalculationType
  dataType: DataValueType
  unit?: string
  isEnabled?: boolean
}

/**
 * 更新虚拟数据点请求
 */
export interface UpdateVirtualDataPointRequest {
  id: number
  deviceId: number
  name: string
  tag: string
  description?: string
  expression: string
  calculationType: CalculationType
  dataType: DataValueType
  unit?: string
  isEnabled: boolean
}
