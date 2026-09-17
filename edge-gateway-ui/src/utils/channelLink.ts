/**
 * 根据当前浏览器访问地址生成服务端通道的可访问链接。
 * 后端 HTTP 服务端和 WebSocket 服务端都复用网关的 Web 端口。
 */
export interface ChannelLinkSource {
  protocol?: number | string | null
  protocolValue?: number | null
  endpoint?: string | null
  httpMode?: string | null
  wsSubscribeTopic?: string | null
}

const getProtocolValue = (channel: ChannelLinkSource) => {
  const value = channel.protocolValue ?? channel.protocol ?? null
  return typeof value === 'number' ? value : Number(value)
}

const getHttpServerPath = (endpoint?: string | null) => {
  const value = endpoint?.trim() ?? ''
  if (value.toLowerCase().startsWith('/api/http-data/')) {
    return value
  }

  return `/api/http-data/${value.replace(/^\/+/, '') || 'xxx'}`
}

/** 获取 HTTP 服务端或 WebSocket 服务端的订阅链接。 */
export const getChannelLink = (channel: ChannelLinkSource): string | null => {
  if (typeof window === 'undefined') return null

  const protocol = getProtocolValue(channel)
  if (protocol === 2 && channel.httpMode === 'server') {
    return `${window.location.origin}${getHttpServerPath(channel.endpoint)}`
  }

  if (protocol === 5) {
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
    const topic = channel.wsSubscribeTopic?.trim() || 'device/data'
    return `${wsProtocol}//${window.location.host}/ws?topic=${encodeURIComponent(topic)}`
  }

  return null
}

/** 复制链接，兼容非 HTTPS 页面无法使用 Clipboard API 的情况。 */
export const copyText = async (value: string) => {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value)
    return
  }

  const textarea = document.createElement('textarea')
  textarea.value = value
  textarea.setAttribute('readonly', '')
  textarea.style.position = 'fixed'
  textarea.style.opacity = '0'
  document.body.appendChild(textarea)
  textarea.select()

  try {
    if (!document.execCommand('copy')) {
      throw new Error('浏览器不支持复制')
    }
  } finally {
    document.body.removeChild(textarea)
  }
}
