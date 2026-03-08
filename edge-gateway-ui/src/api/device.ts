import request from './request'

/** 获取所有设备列表 */
export const getDevices = () => request.get('/devices')

/** 根据ID获取设备详情 */
export const getDevice = (id) => request.get(`/devices/${id}`)

/** 新增设备 */
export const createDevice = (data) => request.post('/devices', data)

/** 更新设备 */
export const updateDevice = (id, data) => request.put(`/devices/${id}`, data)

/** 删除设备 */
export const deleteDevice = (id) => request.delete(`/devices/${id}`)

/** 启用/禁用设备 */
export const toggleDevice = (id) => request.patch(`/devices/${id}/toggle`)

/** 获取设备下的数据点（分页） */
export const getDataPointsPaged = (deviceId, params?: {
  page?: number
  pageSize?: number
  search?: string
  dataType?: number
  isEnabled?: boolean
}) =>
  request.get(`/devices/${deviceId}/datapoints/paged`, { params })

/** 获取设备下的数据点 */
export const getDataPoints = (deviceId) =>
  request.get(`/devices/${deviceId}/datapoints`)

/** 获取所有数据点 */
export const getAllDataPoints = () =>
  request.get('/devices/datapoints')

/** 新增数据点 */
export const createDataPoint = (deviceId, data) =>
  request.post(`/devices/${deviceId}/datapoints`, data)

/** 删除数据点 */
export const deleteDataPoint = (deviceId, dataPointId) =>
  request.delete(`/devices/${deviceId}/datapoints/${dataPointId}`)

/** 更新数据点 */
export const updateDataPoint = (deviceId, dataPointId, data) =>
  request.put(`/devices/${deviceId}/datapoints/${dataPointId}`, data)

/** 切换数据点启用状态 */
export const toggleDataPoint = (deviceId, dataPointId, isEnabled) =>
  request.put(`/devices/${deviceId}/datapoints/${dataPointId}`, { isEnabled })

/** 获取设备数据点的实时数据 */
export const getDeviceRealtimeData = (deviceId) =>
  request.get(`/devices/${deviceId}/datapoints/realtime`)
