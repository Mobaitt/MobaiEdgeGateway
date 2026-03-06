import request from './request'
import type { VirtualDataPoint, CreateVirtualDataPointRequest, UpdateVirtualDataPointRequest } from '@/types/virtualNode'

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
