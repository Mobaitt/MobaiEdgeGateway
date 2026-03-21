// =============================================
// 采集协议枚举（与后端 CollectionProtocol 对应）
// =============================================
export const CollectionProtocol = {
  Simulator: { value: 0, label: 'Simulator', color: '#38dcc4', desc: '模拟器' },
  Modbus:    { value: 1, label: 'Modbus', color: '#4299e1', desc: '工业协议' },
  OpcUa:     { value: 2, label: 'OPC UA', color: '#9f7aea', desc: '工业自动化' },
  Virtual:   { value: 3, label: 'Virtual', color: '#48bb78', desc: '计算节点' },
  S7:        { value: 4, label: 'Siemens S7', color: '#ed8936', desc: '西门子 PLC' }
}

export const CollectionProtocolOptions = Object.values(CollectionProtocol).map(p => ({
  label: p.label, value: p.value, desc: p.desc
}))

// =============================================
// 发送协议枚举（与后端 SendProtocol 对应）
// =============================================
export const SendProtocol = {
  Mqtt:      { value: 1, label: 'MQTT',       icon: 'Connection', color: '#f6b73c', desc: '发布/订阅模式' },
  Http:      { value: 2, label: 'HTTP',        icon: 'Link',       color: '#4299e1', desc: '客户端/服务端模式' },
  Kafka:     { value: 3, label: 'Kafka',       icon: 'MessageBox', color: '#9f7aea', desc: '消息队列' },
  LocalFile: { value: 4, label: '本地文件',    icon: 'FolderOpened', color: '#48bb78', desc: 'NDJSON 格式' },
  WebSocket: { value: 5, label: 'WebSocket',   icon: 'Refresh',    color: '#38dcc4', desc: '服务端推送' }
}

export const SendProtocolOptions = Object.values(SendProtocol).map(p => ({
  label: p.label, value: p.value, desc: p.desc
}))

// =============================================
// 数据值类型枚举
// =============================================
export const DataValueType = {
  Bool:   { value: 1, label: 'Bool' },
  Int16:  { value: 2, label: 'Int16' },
  Int32:  { value: 3, label: 'Int32' },
  Int64:  { value: 4, label: 'Int64' },
  Float:  { value: 5, label: 'Float' },
  Double: { value: 6, label: 'Double' },
  String: { value: 7, label: 'String' }
}

export const DataValueTypeOptions = Object.values(DataValueType).map(t => ({
  label: t.label, value: t.value
}))

// =============================================
// 辅助函数
// =============================================

/** 根据协议value获取对应配置 */
export const getProtocolConfig = (value, map = CollectionProtocol) =>
  Object.values(map).find(p => p.value === value) || { label: String(value), color: '#8fa5c5' }

/** 格式化日期时间 */
export const formatDateTime = (dateStr) => {
  if (!dateStr) return '-'
  const d = new Date(dateStr)
  return d.toLocaleString('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit'
  })
}

/** 格式化毫秒为可读周期 */
export const formatInterval = (ms) => {
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${ms / 1000}s`
  return `${ms / 60000}min`
}
