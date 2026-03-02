import request from './request'
import type { VirtualDataPoint, CreateVirtualDataPointRequest, UpdateVirtualDataPointRequest, VirtualNodeCalculationResult } from '@/types/virtualNode'

/**
 * 获取所有虚拟数据点
 */
export function getVirtualDataPoints() {
  return request<VirtualDataPoint[]>({
    url: '/virtualnodes/points',
    method: 'get'
  })
}

/**
 * 获取指定设备的虚拟数据点
 */
export function getVirtualDataPointsByDevice(deviceId: number) {
  return request<VirtualDataPoint[]>({
    url: `/virtualnodes/devices/${deviceId}/points`,
    method: 'get'
  })
}

/**
 * 获取虚拟数据点
 */
export function getVirtualDataPoint(id: number) {
  return request<VirtualDataPoint>({
    url: `/virtualnodes/points/${id}`,
    method: 'get'
  })
}

/**
 * 创建虚拟数据点
 */
export function createVirtualDataPoint(data: CreateVirtualDataPointRequest) {
  return request<VirtualDataPoint>({
    url: '/virtualnodes/points',
    method: 'post',
    data
  })
}

/**
 * 更新虚拟数据点
 */
export function updateVirtualDataPoint(id: number, data: UpdateVirtualDataPointRequest) {
  return request<VirtualDataPoint>({
    url: `/virtualnodes/points/${id}`,
    method: 'put',
    data
  })
}

/**
 * 删除虚拟数据点
 */
export function deleteVirtualDataPoint(id: number) {
  return request({
    url: `/virtualnodes/points/${id}`,
    method: 'delete'
  })
}

/**
 * 计算虚拟数据点
 */
export function calculateVirtualDataPoint(id: number) {
  return request<VirtualNodeCalculationResult>({
    url: `/virtualnodes/points/${id}/calculate`,
    method: 'post'
  })
}

/**
 * 计算设备下所有启用的虚拟数据点
 */
export function calculateDeviceVirtualDataPoints(deviceId: number) {
  return request<VirtualNodeCalculationResult[]>({
    url: `/virtualnodes/devices/${deviceId}/calculate`,
    method: 'post'
  })
}

/**
 * 计算所有启用的虚拟数据点
 */
export function calculateAllVirtualDataPoints() {
  return request<VirtualNodeCalculationResult[]>({
    url: '/virtualnodes/points/calculate-all',
    method: 'post'
  })
}

/**
 * 解析表达式依赖
 */
export function parseDependencies(expression: string) {
  return request<string[]>({
    url: '/virtualnodes/parse-dependencies',
    method: 'post',
    data: expression
  })
}
