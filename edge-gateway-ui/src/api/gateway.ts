import request from './request'

/** 获取网关整体运行状态 */
export const getGatewayStatus = () => request.get('/gateway/status')

/** 健康检查 */
export const healthCheck = () => request.get('/gateway/health')

/** 停止指定设备采集 */
export const stopDeviceCollection = (deviceId) =>
  request.post(`/gateway/devices/${deviceId}/stop`)
