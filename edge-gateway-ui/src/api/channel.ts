import request from './request'

/** 获取所有发送通道 */
export const getChannels = () => request.get('/channels')

/** 新增发送通道 */
export const createChannel = (data) => request.post('/channels', data)

/** 更新发送通道 */
export const updateChannel = (channelId, data) => request.put(`/channels/${channelId}`, data)

/** 删除发送通道 */
export const deleteChannel = (channelId) => request.delete(`/channels/${channelId}`)

/** 批量绑定数据点到通道 */
export const bindDataPoints = (channelId, dataPointIds) =>
  request.post(`/channels/${channelId}/bind-datapoints`, { dataPointIds })

/** 获取通道的数据点映射列表 */
export const getMappings = (channelId) =>
  request.get(`/channels/${channelId}/mappings`)

/** 删除映射关系 */
export const deleteMapping = (channelId, mappingId) =>
  request.delete(`/channels/${channelId}/mappings/${mappingId}`)

/** 启用/停用发送通道 */
export const toggleChannel = (channelId) =>
  request.patch(`/channels/${channelId}/toggle`)
