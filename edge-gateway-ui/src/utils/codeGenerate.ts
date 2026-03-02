import { pinyin } from 'pinyin-pro'

/**
 * 将名称转为编码：中文只取拼音首字母并连写（如 魂牵梦萦 -> HQMY），英文数字保留并规范化
 * @param name 名称（设备名、通道名、数据点名等）
 * @param defaultPrefix 当结果为空时返回的默认前缀（如 'DEV'、'CH'）
 */
export function nameToCode(name: string, defaultPrefix = 'DEV'): string {
  if (!name || !name.trim()) return defaultPrefix
  const trimmed = name.trim()
  const withPinyin = pinyin(trimmed, { pattern: 'first', toneType: 'none' })
  const code = withPinyin
    .replace(/\s+/g, '')
    .replace(/[^A-Za-z0-9]/g, '_')
    .replace(/_+/g, '_')
    .replace(/^_|_$/g, '')
    .toUpperCase()
    .substring(0, 30)
  return code || defaultPrefix
}

/**
 * 根据名称生成带时间戳的唯一编码（用于设备编码、通道编码等）
 */
export function generateCodeWithTimestamp(name: string, defaultPrefix = 'DEV'): string {
  const base = nameToCode(name, defaultPrefix)
  const timestamp = Date.now().toString(36).toUpperCase()
  return `${base}_${timestamp}`
}
