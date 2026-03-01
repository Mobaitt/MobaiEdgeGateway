import request from './request'

/** 获取采集协议选项 */
export const getCollectionProtocols = () => request.get('/enums/collection-protocols')

/** 获取发送协议选项 */
export const getSendProtocols = () => request.get('/enums/send-protocols')

/** 获取数据类型选项 */
export const getDataValueTypes = () => request.get('/enums/data-value-types')
