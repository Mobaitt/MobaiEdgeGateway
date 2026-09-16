import request from './request'

export interface DataPointTemplateItem {
  id: number
  name: string
  description?: string | null
  protocol: string
  pointCount: number
  createdAt: string
  updatedAt: string
}

export interface DataPointTemplatePoint {
  name: string
  tagSuffix: string
  description?: string | null
  address: string
  dataType: number
  unit?: string | null
  modbusSlaveId?: number | null
  modbusFunctionCode?: number | null
  modbusByteOrder?: number | null
  registerLength: number
  modbusBitIndex?: number | null
  isEnabled: boolean
  isControllable: boolean
}

export interface DataPointTemplateDetail extends DataPointTemplateItem {
  points: DataPointTemplatePoint[]
}

export const getDataPointTemplates = (protocol?: number) =>
  request.get('/datapoint-templates', { params: protocol === undefined ? undefined : { protocol } })

export const createDataPointTemplateFromDevice = (deviceId: number, data: { name: string; description?: string }) =>
  request.post(`/datapoint-templates/from-device/${deviceId}`, data)

export const applyDataPointTemplate = (deviceId: number, templateId: number, overwriteExisting = false) =>
  request.post(`/devices/${deviceId}/datapoints/templates/${templateId}/apply`, { overwriteExisting })

export const deleteDataPointTemplate = (templateId: number) =>
  request.delete(`/datapoint-templates/${templateId}`)

export const getDataPointTemplatesPaged = (params: { page?: number; pageSize?: number; search?: string; protocol?: number }) =>
  request.get('/datapoint-templates/paged', { params })

export const getDataPointTemplate = (templateId: number) =>
  request.get(`/datapoint-templates/${templateId}`)

export const updateDataPointTemplate = (templateId: number, data: { name: string; description?: string; points: DataPointTemplatePoint[] }) =>
  request.put(`/datapoint-templates/${templateId}`, data)
